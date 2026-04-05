using System;
using System.IO;
using System.Security.Cryptography;
using ScriviTest.DTOs;
using System.IO.Compression;
using System.Text.Json;

namespace ScriviTest.Services;

public class CryptographyService
{
    // Generates a random 6-character code (Avoiding ambiguous letters like I, O, 1, 0)
    public string GenerateExaminationKey()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        var key = new char[6];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = chars[random.Next(chars.Length)];
        }
        return new string(key);
    }

    // Encrypts the .xamn file directly on the hard drive
    // Encrypts the .xamn file directly on the hard drive
    public void EncryptFile(string filePath, string password)
    {
        string tempPath = filePath + ".tmp";
        
        // 1. Generate a random Salt to prevent dictionary attacks
        byte[] salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        // 2. Stretch the password into 48 bytes using the modern static method
        // (32 bytes for the AES-256 Key + 16 bytes for the IV)
        byte[] derivedBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100000,
            HashAlgorithmName.SHA256,
            48 
        );

        // Slice the array cleanly using C# range operators
        byte[] key = derivedBytes[0..32];
        byte[] iv = derivedBytes[32..48];

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            // 3. Securely stream the file through the encryption algorithm
            using (var fsOut = new FileStream(tempPath, FileMode.Create))
            {
                // Write the salt to the very beginning of the file so the Examinee app can read it to decrypt
                fsOut.Write(salt, 0, salt.Length);
                
                using (var cryptoStream = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    using (var fsIn = new FileStream(filePath, FileMode.Open))
                    {
                        fsIn.CopyTo(cryptoStream);
                    }
                }
            }
        }
        
        // 4. Overwrite the unencrypted file with the encrypted payload
        File.Move(tempPath, filePath, true);
    }

    // Decrypts the file, extracts the images to a secure temp folder, and parses the JSON
    public StudentExamDto? DecryptAndExtractExam(string encryptedFilePath, string password, string tempImageDirectory)
    {
        try
        {
            using var fsIn = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read);
            
            // 1. Read the 16-byte salt we planted at the very beginning of the file
            byte[] salt = new byte[16];
            fsIn.ReadExactly(salt, 0, salt.Length);

            // 2. Stretch the user's 6-character input back into the full 48-byte key/IV
            byte[] derivedBytes = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 48);
            byte[] key = derivedBytes[0..32];
            byte[] iv = derivedBytes[32..48];

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            // 3. Decrypt the stream directly into RAM (MemoryStream)
            using var cryptoStream = new CryptoStream(fsIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var memoryStream = new MemoryStream();
            cryptoStream.CopyTo(memoryStream);
            memoryStream.Position = 0; // Reset the "cursor" to the start of the memory block

            // 4. Open the decrypted Zip Archive in RAM
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            
            // Create the temporary folder for this specific exam session's images
            Directory.CreateDirectory(tempImageDirectory);

            // 5. Extract Images to the Temp Folder
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("media/") && !string.IsNullOrEmpty(entry.Name))
                {
                    string destPath = Path.Combine(tempImageDirectory, entry.Name);
                    entry.ExtractToFile(destPath, true);
                }
            }

            // 6. Read and Deserialize the JSON file
            var jsonEntry = archive.GetEntry("exam_data.json");
            if (jsonEntry == null) return null;

            using var reader = new StreamReader(jsonEntry.Open());
            string jsonContent = reader.ReadToEnd();
            
            return JsonSerializer.Deserialize<StudentExamDto>(jsonContent);
        }
        catch (CryptographicException)
        {
            // This specifically catches wrong passwords!
            return null; 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Decryption Failed: {ex.Message}");
            return null;
        }
    }

}