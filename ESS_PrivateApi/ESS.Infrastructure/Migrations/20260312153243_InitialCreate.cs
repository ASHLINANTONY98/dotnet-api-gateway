using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ESS_SOFT_TOKENS",
                columns: table => new
                {
                    EMP_CODE = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TOKEN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GENERATED_ON = table.Column<DateTime>(type: "datetime2", nullable: false),
                    STATUS = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESS_SOFT_TOKENS", x => x.EMP_CODE);
                });

            migrationBuilder.CreateTable(
                name: "VENDOR_MASTER_ESS",
                columns: table => new
                {
                    VENDOR_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VENDOR_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    API_KEY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IS_ACTIVE = table.Column<int>(type: "int", nullable: false),
                    VENDOR_ROLE = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VENDOR_MASTER_ESS", x => x.VENDOR_ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ESS_SOFT_TOKENS");

            migrationBuilder.DropTable(
                name: "VENDOR_MASTER_ESS");
        }
    }
}
