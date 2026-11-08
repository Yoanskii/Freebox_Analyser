# Dépendances et licences

- .NET 10 / WPF / Windows Forms : Microsoft, MIT (composants Windows selon licence Windows).
- Microsoft.Data.Sqlite 10.0.14 et dépendances : MIT.
- SQLite : domaine public. SQLitePCLRaw : Apache-2.0.
- xUnit : Apache-2.0 ; runner Visual Studio : Apache-2.0 ; Microsoft.NET.Test.Sdk : MIT (développement seulement).
- Inno Setup 6 : licence Inno Setup, https://jrsoftware.org/files/is/license.txt ; outil de construction séparé, non embarqué comme bibliothèque. Vérifier les conditions applicables à votre usage/distribution.
- Graphiques : contrôle WPF du projet, sans bibliothèque tierce.

Les licences et copyrights des runtimes redistribués restent applicables. Exécuter `dotnet list package --include-transitive` pour la liste exacte. Le pipeline audite les dépendances NuGet à la restauration ; aucun avertissement de vulnérabilité n’est supprimé.
