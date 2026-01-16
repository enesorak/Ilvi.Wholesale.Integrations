using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ilvi.Modules.AmoCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePipelineStatusesToStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Statuses",
                table: "Pipelines",
                newName: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Pipelines",
                newName: "Statuses");
        }
    }
}
