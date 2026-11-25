using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartlog.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Intervenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RaisonSociale = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Coordonnees = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Intervenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    PermissionsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Intitule = table.Column<string>(type: "TEXT", nullable: false),
                    MaitreOuvrageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaitreOeuvreId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EntrepriseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Localisation = table.Column<string>(type: "TEXT", nullable: true),
                    NatureTravaux = table.Column<string>(type: "TEXT", nullable: true),
                    MontantInitial = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MontantActualise = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DateNotification = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DateCommencement = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DelaiContractuelJours = table.Column<int>(type: "INTEGER", nullable: false),
                    DelaiActualiseJours = table.Column<int>(type: "INTEGER", nullable: false),
                    DatePrevisionnelleAchevement = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DateReelleAchevement = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projets_Intervenants_EntrepriseId",
                        column: x => x.EntrepriseId,
                        principalTable: "Intervenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projets_Intervenants_MaitreOeuvreId",
                        column: x => x.MaitreOeuvreId,
                        principalTable: "Intervenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projets_Intervenants_MaitreOuvrageId",
                        column: x => x.MaitreOuvrageId,
                        principalTable: "Intervenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Identifiant = table.Column<string>(type: "TEXT", nullable: false),
                    MotDePasseHash = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Utilisateurs_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alertes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntiteSourceType = table.Column<string>(type: "TEXT", nullable: false),
                    EntiteSourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateEcheance = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Priorite = table.Column<int>(type: "INTEGER", nullable: false),
                    EstLue = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alertes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alertes_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachements_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Courriers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sens = table.Column<int>(type: "INTEGER", nullable: false),
                    Expediteur = table.Column<string>(type: "TEXT", nullable: false),
                    Destinataire = table.Column<string>(type: "TEXT", nullable: false),
                    Objet = table.Column<string>(type: "TEXT", nullable: false),
                    DelaiReponseJours = table.Column<int>(type: "INTEGER", nullable: true),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courriers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courriers_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TypeDocument = table.Column<string>(type: "TEXT", nullable: false),
                    CheminFichier = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ContenuOcr = table.Column<string>(type: "TEXT", nullable: true),
                    EntiteLieeType = table.Column<string>(type: "TEXT", nullable: true),
                    EntiteLieeId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Marches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceMarche = table.Column<string>(type: "TEXT", nullable: false),
                    DateSignature = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Marches_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reserves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Priorite = table.Column<int>(type: "INTEGER", nullable: false),
                    EntrepriseResponsableId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DateConstatation = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DelaiLeveeJours = table.Column<int>(type: "INTEGER", nullable: false),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false),
                    DateLevee = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reserves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reserves_Intervenants_EntrepriseResponsableId",
                        column: x => x.EntrepriseResponsableId,
                        principalTable: "Intervenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reserves_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Situations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Periode = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    MontantCumule = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RetenueGarantie = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Avance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Penalites = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Situations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Situations_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Taches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentTacheId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Libelle = table.Column<string>(type: "TEXT", nullable: false),
                    DateDebutPrevue = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateFinPrevue = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateDebutReelle = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DateFinReelle = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AvancementPct = table.Column<decimal>(type: "TEXT", nullable: false),
                    EstCheminCritique = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Taches_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Taches_Taches_ParentTacheId",
                        column: x => x.ParentTacheId,
                        principalTable: "Taches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntiteType = table.Column<string>(type: "TEXT", nullable: false),
                    EntiteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    ValeurAvantJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValeurApresJson = table.Column<string>(type: "TEXT", nullable: true),
                    DateAction = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalActions_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Avenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarcheId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    MontantDelta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DelaiDeltaJours = table.Column<int>(type: "INTEGER", nullable: false),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false),
                    DateValidation = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    JustificatifDocumentId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Avenants_Documents_JustificatifDocumentId",
                        column: x => x.JustificatifDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Avenants_Marches_MarcheId",
                        column: x => x.MarcheId,
                        principalTable: "Marches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrixUnitaires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarcheId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", nullable: false),
                    Designation = table.Column<string>(type: "TEXT", nullable: false),
                    Unite = table.Column<string>(type: "TEXT", nullable: false),
                    PrixUnitaireHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    QuantiteContractuelle = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrixUnitaires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrixUnitaires_Marches_MarcheId",
                        column: x => x.MarcheId,
                        principalTable: "Marches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiaisonsTache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TachePredecesseurId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TacheSuccesseurId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TypeLiaison = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiaisonsTache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiaisonsTache_Taches_TachePredecesseurId",
                        column: x => x.TachePredecesseurId,
                        principalTable: "Taches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiaisonsTache_Taches_TacheSuccesseurId",
                        column: x => x.TacheSuccesseurId,
                        principalTable: "Taches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LignesAttachement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttachementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrixUnitaireId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuantiteExecutee = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    MontantCalcule = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesAttachement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesAttachement_Attachements_AttachementId",
                        column: x => x.AttachementId,
                        principalTable: "Attachements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LignesAttachement_PrixUnitaires_PrixUnitaireId",
                        column: x => x.PrixUnitaireId,
                        principalTable: "PrixUnitaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alertes_ProjetId",
                table: "Alertes",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachements_ProjetId",
                table: "Attachements",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Avenants_JustificatifDocumentId",
                table: "Avenants",
                column: "JustificatifDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Avenants_MarcheId",
                table: "Avenants",
                column: "MarcheId");

            migrationBuilder.CreateIndex(
                name: "IX_Courriers_ProjetId",
                table: "Courriers",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjetId",
                table: "Documents",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalActions_UtilisateurId",
                table: "JournalActions",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_LiaisonsTache_TachePredecesseurId",
                table: "LiaisonsTache",
                column: "TachePredecesseurId");

            migrationBuilder.CreateIndex(
                name: "IX_LiaisonsTache_TacheSuccesseurId",
                table: "LiaisonsTache",
                column: "TacheSuccesseurId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesAttachement_AttachementId",
                table: "LignesAttachement",
                column: "AttachementId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesAttachement_PrixUnitaireId",
                table: "LignesAttachement",
                column: "PrixUnitaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Marches_ProjetId",
                table: "Marches",
                column: "ProjetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrixUnitaires_MarcheId",
                table: "PrixUnitaires",
                column: "MarcheId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_Code",
                table: "Projets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projets_EntrepriseId",
                table: "Projets",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_MaitreOeuvreId",
                table: "Projets",
                column: "MaitreOeuvreId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_MaitreOuvrageId",
                table: "Projets",
                column: "MaitreOuvrageId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserves_EntrepriseResponsableId",
                table: "Reserves",
                column: "EntrepriseResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserves_ProjetId",
                table: "Reserves",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Situations_ProjetId",
                table: "Situations",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Taches_ParentTacheId",
                table: "Taches",
                column: "ParentTacheId");

            migrationBuilder.CreateIndex(
                name: "IX_Taches_ProjetId",
                table: "Taches",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_RoleId",
                table: "Utilisateurs",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alertes");

            migrationBuilder.DropTable(
                name: "Avenants");

            migrationBuilder.DropTable(
                name: "Courriers");

            migrationBuilder.DropTable(
                name: "JournalActions");

            migrationBuilder.DropTable(
                name: "LiaisonsTache");

            migrationBuilder.DropTable(
                name: "LignesAttachement");

            migrationBuilder.DropTable(
                name: "Reserves");

            migrationBuilder.DropTable(
                name: "Situations");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Utilisateurs");

            migrationBuilder.DropTable(
                name: "Taches");

            migrationBuilder.DropTable(
                name: "Attachements");

            migrationBuilder.DropTable(
                name: "PrixUnitaires");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Marches");

            migrationBuilder.DropTable(
                name: "Projets");

            migrationBuilder.DropTable(
                name: "Intervenants");
        }
    }
}
