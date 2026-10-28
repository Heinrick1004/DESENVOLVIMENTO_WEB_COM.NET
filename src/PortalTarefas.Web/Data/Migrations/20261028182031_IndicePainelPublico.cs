using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalTarefas.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndicePainelPublico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_Publica_Prazo",
                table: "Tarefas",
                columns: new[] { "Publica", "Prazo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tarefas_Publica_Prazo",
                table: "Tarefas");
        }
    }
}
