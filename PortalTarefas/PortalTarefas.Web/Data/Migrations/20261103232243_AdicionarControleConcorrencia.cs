using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalTarefas.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarControleConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Versao",
                table: "Tarefas",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Tarefas");
        }
    }
}
