ScriviTest 


ScriviTest provides educators with a robust tool to design complex exams (Multiple Choice, True/False, Essay) while enforcing anti-cheat strictness and time limits. It features a custom encrypted file format (.xans) to securely package and export student submissions for grading.


Key Features:

--Dynamic Exam Creation: Build custom exams with mixed question types, customizable points, and adjustable time limits.

--Secure Encrypted Submissions: Student answers are compiled into a custom .xans file format, serialized via JSON, and secured with file-level encryption to prevent tampering.

--Cross-Platform UI: Built with Avalonia UI, ensuring the application looks and functions identically across Windows, macOS, and Linux.

--Custom Theming: Fully responsive Light and Dark modes built using Avalonia Resource Dictionaries.

--Memory-Optimized Image Handling: Engineered with strict memory management protocols and explicit garbage collection loops to handle high-definition unmanaged image assets without memory leaks.

--Self-Contained Typography: Bypasses OS-level font dependencies by bundling Microsoft's Fluent System Icons directly into the compiled application assembly.

Tech Stack & Architecture

--Framework: .NET 10 / C#

--UI Presentation: Avalonia UI

--Architecture: MVVM (Model-View-ViewModel) utilizing CommunityToolkit.Mvvm

--Data Handling: JSON Serialization

--Security: Cryptography APIs for file-level encryption


Getting Started

Prerequisites
To build and run this project from the source code, you will need:

--.NET 10.0 SDK

--An IDE such as Visual Studio, JetBrains Rider, or VS Code with the Avalonia extension.

Installation & Build
1) Clone the repository: git clone https://github.com/YourUsername/ScriviTest.git
2) Navigate to the project directory: cd ScriviTest
3) Restore dependencies and build the application: dotnet build
4) Run the application: dotnet run

Technical Highlights (For Developers)

--The .xans File System: Submissions aren't just saved as raw text. The SubmitExam() method strips the UI logic, compiles the raw data (names, IDs, selected indices, essay strings) into a StudentSubmissionDto, serializes it, and encrypts the output file to protect student data integrity.

--Memory Leak Resolution: Early versions suffered from UI lag due to high-resolution image caching. This was solved by implementing explicit Dispose() loops that destroy unmanaged bitmaps from RAM immediately prior to directory cleanup.
