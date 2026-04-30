using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UniversityScheduler.Data
{
   public static class LicenseManager
{
        
      private static readonly string _publicKey = @"
      -----BEGIN PUBLIC KEY-----
      
      -----END PUBLIC KEY-----";
      private static readonly string _basePath = AppDomain.CurrentDomain.BaseDirectory;
      private static readonly string _licenseFile = Path.Combine(_basePath, "license.dat");
      private static readonly string _idFile = Path.Combine(_basePath, "device_id.dat");
      private static readonly string _sysCacheFile = Path.Combine(_basePath, "sys_cache.dat"); 

      private static void UpdateLastRunDate()
      {
      try 
      { 
         File.WriteAllText(_sysCacheFile, DateTime.Now.ToString()); 
      } 
      catch { }
      }

      private static bool IsClockTampered()
      {
      try
      {
         if (File.Exists(_sysCacheFile))
         {
               DateTime lastRun = DateTime.Parse(File.ReadAllText(_sysCacheFile));
               if (DateTime.Now < lastRun) return true; 
         }
      }
      catch { } 
      return false;
      }

      public static string GetInstallationId()
      {
      if (File.Exists(_idFile)) return File.ReadAllText(_idFile);
      string newId = "UID-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
      File.WriteAllText(_idFile, newId);
      return newId;
      }

      public static bool IsLicenseValid()
      {
         if (!File.Exists(_licenseFile)) return false;

         try 
         {
            // 1. First, check if the crypto signature is valid
            bool isCryptoValid = VerifyLicense(File.ReadAllText(_licenseFile));
            
            // If crypto is bad, fail immediately
            if (!isCryptoValid) return false;

            // 2. Now check for Time Travel
            if (IsClockTampered()) return false;

            // 3. Update the cache and return true
            UpdateLastRunDate();
            return true;
         }
         catch
         {
            return false;
         }
      }

      public static DateTime GetExpirationDate()
      {
         // 1. If no file exists, return MinValue (expired)
         if (!File.Exists(_licenseFile)) return DateTime.MinValue;

         try
         {
            // 2. Read the file content
            string licenseString = File.ReadAllText(_licenseFile);

            // 3. Decode the Base64 wrapper
            // The format is: Base64( "YYYY-MM-DD" + "|" + Signature )
            string decodedRaw = Encoding.UTF8.GetString(Convert.FromBase64String(licenseString));
            string[] parts = decodedRaw.Split('|');

            // 4. Extract and Parse the Date (Part 0)
            if (parts.Length > 0 && DateTime.TryParse(parts[0], out DateTime expiry))
            {
                  return expiry;
            }
         }
         catch 
         {
            // If file is corrupted or tampered, assume invalid
         }

         return DateTime.MinValue;
      }
      
      public static bool TryActivate(string activationKey)
      {
      if (VerifyLicense(activationKey))
      {
            File.WriteAllText(_licenseFile, activationKey);
            return true;
      }
      return false;
      }

      private static bool VerifyLicense(string licenseString)
      {
      try
      {
            // Step A: Unwrap the package
            // Format: Base64( "YYYY-MM-DD" + "|" + Base64(Signature) )
            string decodedRaw = Encoding.UTF8.GetString(Convert.FromBase64String(licenseString));
            string[] parts = decodedRaw.Split('|');
            
            if (parts.Length != 2) return false;

            string expiryDateStr = parts[0];
            string signatureBase64 = parts[1];
            byte[] signature = Convert.FromBase64String(signatureBase64);

            // Step B: Reconstruct the data that WAS signed (ID + Date)
            // This must match exactly what Python signed: "RequestCode|ExpiryDate"
            string machineId = GetInstallationId();
            byte[] originalData = Encoding.UTF8.GetBytes($"{machineId}|{expiryDateStr}");

            // Step C: Verify with Public Key
            using (RSA rsa = RSA.Create())
            {
               rsa.ImportFromPem(_publicKey);
               
               bool isSignatureValid = rsa.VerifyData(
                  originalData, 
                  signature, 
                  HashAlgorithmName.SHA256, 
                  RSASignaturePadding.Pkcs1
               );

               if (!isSignatureValid) return false; // signature mismatch (HACK DETECTED)
            }

            // Step D: Check Date
            if (DateTime.TryParse(expiryDateStr, out DateTime expiry))
            {
               return DateTime.Now <= expiry;
            }
      }
      catch 
      {
            // Any error (bad format, tampered string) returns false
      }
      return false;
      }
   
   }
}