using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_BayId_StartTimeUtc",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_TechnicianId_StartTimeUtc",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_ActiveBayStart",
                table: "Appointments",
                columns: new[] { "BayId", "StartTimeUtc" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [Status] <> 'NoShow'");

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_ActiveTechnicianStart",
                table: "Appointments",
                columns: new[] { "TechnicianId", "StartTimeUtc" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [Status] <> 'NoShow'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Appointments_ActiveBayStart",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_ActiveTechnicianStart",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BayId_StartTimeUtc",
                table: "Appointments",
                columns: new[] { "BayId", "StartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TechnicianId_StartTimeUtc",
                table: "Appointments",
                columns: new[] { "TechnicianId", "StartTimeUtc" });
        }
    }
}
