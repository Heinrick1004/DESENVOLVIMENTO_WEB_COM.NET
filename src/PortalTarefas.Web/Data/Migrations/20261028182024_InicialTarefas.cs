using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalTarefas.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InicialTarefas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Acao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TarefaId = table.Column<int>(type: "int", nullable: true),
                    Instante = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tarefas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Situacao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Prioridade = table.Column<int>(type: "int", nullable: false),
                    Prazo = table.Column<DateOnly>(type: "date", nullable: true),
                    Equipe = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Publica = table.Column<bool>(type: "bit", nullable: false),
                    ProprietarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarefas", x => x.Id);
                    table.CheckConstraint("CK_Tarefas_Prioridade", "[Prioridade] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Tarefas_Situacao", "[Situacao] IN ('Pendente','EmAndamento','Concluida')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_TarefaId_Instante",
                table: "Auditorias",
                columns: new[] { "TarefaId", "Instante" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_Equipe_Situacao_Prazo",
                table: "Tarefas",
                columns: new[] { "Equipe", "Situacao", "Prazo" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_ProprietarioId",
                table: "Tarefas",
                column: "ProprietarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "Tarefas");
        }
    }
}
