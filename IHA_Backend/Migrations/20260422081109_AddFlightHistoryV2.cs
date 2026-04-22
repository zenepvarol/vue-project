using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IHA_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightHistoryV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tablo veritabanında zaten mevcut olduğu için boş
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma durumunda tabloyu silmemesi için boş
        }
    }
}
