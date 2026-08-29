using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CentralWikiMcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubjectKind = table.Column<int>(type: "integer", nullable: false),
                    Tool = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    SafeParameters = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ParametersHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BsnOperationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_states",
                columns: table => new
                {
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PagesIndexed = table.Column<int>(type: "integer", nullable: false),
                    PagesRemoved = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_states", x => x.Source);
                });

            migrationBuilder.CreateTable(
                name: "wiki_pages",
                columns: table => new
                {
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                    Namespace = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(to_tsvector('russian', coalesce(\"Title\", '')), 'A') ||\nsetweight(to_tsvector('russian', coalesce(\"ContentMarkdown\", '')), 'B')", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_pages", x => x.PageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_BsnOperationId",
                table: "audit_events",
                column: "BsnOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OccurredAt",
                table: "audit_events",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_SubjectId",
                table: "audit_events",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_Namespace",
                table: "wiki_pages",
                column: "Namespace");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_Path",
                table: "wiki_pages",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_SearchVector",
                table: "wiki_pages",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_Source",
                table: "wiki_pages",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_Tags",
                table: "wiki_pages",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_UpdatedAt",
                table: "wiki_pages",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "sync_states");

            migrationBuilder.DropTable(
                name: "wiki_pages");
        }
    }
}
