using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmailSender.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlinharIndicesComDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Emails_LoteId",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_Status",
                table: "Emails");

            migrationBuilder.RenameIndex(
                name: "IX_LotesEnvio_IdempotencyKey",
                table: "LotesEnvio",
                newName: "UQ_Lote_IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Email_LoteId_Status",
                table: "Emails",
                columns: new[] { "LoteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Email_Status_CriadoEm",
                table: "Emails",
                columns: new[] { "Status", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "UQ_Email_LoteId_Destinatario",
                table: "Emails",
                columns: new[] { "LoteId", "Destinatario" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Email_LoteId_Status",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Email_Status_CriadoEm",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "UQ_Email_LoteId_Destinatario",
                table: "Emails");

            migrationBuilder.RenameIndex(
                name: "UQ_Lote_IdempotencyKey",
                table: "LotesEnvio",
                newName: "IX_LotesEnvio_IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_LoteId",
                table: "Emails",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_Status",
                table: "Emails",
                column: "Status");
        }
    }
}
