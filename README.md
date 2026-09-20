🎓 EduSathi: AI-Powered Collaborative Learning Platform

An intelligent, real-time collaborative study platform designed to transform dense educational documents into interactive study guides, active-recall flashcards, and multiplayer quiz rooms.
🚀 Key Features

    📄 AI Document Parsing & Summarization: Upload study materials and leverage the Google Gemini API to generate structured, high-yield study guides.

    🧠 Active-Recall Flashcard System: Automatically extracts core concepts into a sleek, card-flipping interface with short, memorable answers to minimize token rate limits and maximize student memory retention.

    ⚡ Real-Time Multiplayer Study Rooms: Built using ASP.NET Core SignalR, allowing students to join live lobbies, invite friends, and compete in synchronized study sessions.

    🎭 Dynamic Persona Switcher: Instantly transforms standard academic summaries into different tones (Gen-Z Slang, Strict Professor, or Pirate Captain) to match individual learning styles.

    🎙️ Text-to-Speech Audio Integration: Built-in browser speech synthesis allowing students to listen to summaries hands-free during late-night cram sessions.

    ⏱️ Persistent Focus Timer: A built-in, layout-integrated study timer backed by localStorage to keep students focused.

    🖨️ PDF & Print Export: Cleanly formatted print views optimized for exporting study guides into offline PDFs.

🛠️ Tech Stack

    Backend: ASP.NET Core MVC (.NET 10.0), C#, Entity Framework Core

    Real-Time Communication: ASP.NET Core SignalR

    Database: Microsoft SQL Server & ASP.NET Core Identity (Authentication)

    AI Engine: Google Gemini API (gemini-1.5-flash)

    Frontend UI: Bootstrap, Razor Views (.cshtml), custom CSS, Marked.js, JavaScript

⚙️ Getting Started Locally
Prerequisites

    .NET 10.0 SDK or higher installed on your machine.

    Microsoft SQL Server (LocalDB or SQL Server Express).

Installation & Running

    Clone the repository:
    Bash

    git clone https://github.com/Nirmal-Rana/Edu_Sathi.git
    cd Edu_Sathi

    Configure your environment settings:
    Create an appsettings.json or user secrets file with your database connection string and Google Gemini API key.

    Run database migrations:
    Bash

    dotnet ef database update

    Run the application:
    Bash

    dotnet run

👨‍💻 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the issues page.
📝 License

This project is open source and available under the MIT License.
