# Findings — Prompt 3

- Workspace racine : `C:\Users\EUROGEN\Desktop\SmartLog`. Code dans `SMARTLOG\src`.
- Solutions : Domain (entités pures), Data (EF Core 8 + SQLite), Services, App (WPF/MVVM).
- Prompt 0 et 1 déjà livrés : migration `20261124195606_Initial`, `DonneesDemonstration`, initialisation branchée dans `App.xaml.cs`.
- Prompt 2 déjà livré : `IProjetService/ProjetService`, `VueProjets`, `VueFicheProjet`, navigation depuis `MainViewModel`.
- `IAvenantService` existe déjà avec `InitialiserProjetAsync`, `ValiderAsync`, `AnnulerAsync`. Les montants/délai actualisés ne doivent être écrits que là.
- `AvenantService.AnnulerAsync` ne recalcule pas `DatePrevisionnelleAchevement` : à corriger.
- `ISituationService` existe déjà et calcule cumul/retenue/avance.
- `JournalService.EnregistrerAsync` est le point de traçabilité unique ; toutes les écritures métier doivent y passer.
- SDK installé : .NET 10.0.404 ; build net8.0 OK au départ (0 erreur).
- Packages NuGet en cache : xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4, EF Core Sqlite 8.0.10.
- Entités utiles : `Marche`, `Avenant`, `PrixUnitaire`, `DocumentGed` (avec `EntiteLieeType/EntiteLieeId` permettant d'associer une pièce au marché).
- UI existante : Menu latéral 14 entrées dans `MainViewModel` ; `MainWindow.xaml` utilise des `DataTemplate` par type de ViewModel.
