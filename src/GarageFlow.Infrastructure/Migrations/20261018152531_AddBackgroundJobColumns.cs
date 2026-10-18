using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OfferExpiresUtc",
                table: "WaitlistEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferNotifiedUtc",
                table: "WaitlistEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferedEndUtc",
                table: "WaitlistEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfferedTechnicianId",
                table: "WaitlistEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentUtc",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_Status_OfferExpiresUtc",
                table: "WaitlistEntries",
                columns: new[] { "Status", "OfferExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_StartTimeUtc",
                table: "Appointments",
                columns: new[] { "Status", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_Status_OfferExpiresUtc",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Status_StartTimeUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "OfferExpiresUtc",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "OfferNotifiedUtc",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "OfferedEndUtc",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "OfferedTechnicianId",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "ReminderSentUtc",
                table: "Appointments");
        }
    }
}
