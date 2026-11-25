using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smartlog.Data.Migrations
{
    /// <inheritdoc />
    public partial class CourrierDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateCourrier",
                table: "Courriers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateReponse",
                table: "Courriers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateCourrier",
                table: "Courriers");

            migrationBuilder.DropColumn(
                name: "DateReponse",
                table: "Courriers");
        }
    }
}
