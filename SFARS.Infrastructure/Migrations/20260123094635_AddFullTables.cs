using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SFARS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshToken_UserId",
                table: "RefreshToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User_UserId",
                table: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SystemMessage_MsgId",
                table: "SystemMessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Snake_SnakeId",
                table: "Snake");

            migrationBuilder.DropIndex(
                name: "IX_Snake_Name",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "User");

            migrationBuilder.DropColumn(
                name: "create_date",
                table: "User");

            migrationBuilder.DropColumn(
                name: "email_confirmed",
                table: "User");

            migrationBuilder.DropColumn(
                name: "email_verification_code",
                table: "User");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "User");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "User");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "User");

            migrationBuilder.DropColumn(
                name: "modified_date",
                table: "User");

            migrationBuilder.DropColumn(
                name: "phone_number_confirmed",
                table: "User");

            migrationBuilder.DropColumn(
                name: "phone_verification_code",
                table: "User");

            migrationBuilder.DropColumn(
                name: "phone_verification_expiry",
                table: "User");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "User");

            migrationBuilder.DropColumn(
                name: "two_factor_backup_codes",
                table: "User");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                table: "User");

            migrationBuilder.DropColumn(
                name: "two_factor_secret_key",
                table: "User");

            migrationBuilder.DropColumn(
                name: "create_by",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "create_date",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "modified_date",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "snake_id",
                table: "Snake");

            migrationBuilder.RenameColumn(
                name: "vietnamese_message",
                table: "SystemMessage",
                newName: "vi");

            migrationBuilder.RenameColumn(
                name: "english_message",
                table: "SystemMessage",
                newName: "en");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Snake",
                newName: "scientific_name");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "User",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "User",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "User",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<DateTime>(
                name: "dob",
                table: "User",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "avatar",
                table: "User",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2048)",
                oldUnicode: false,
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "User",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "User",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "User",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "current_location",
                table: "User",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_online",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_active_at",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "User",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "User",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "msg_content",
                table: "SystemMessage",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "msg_id",
                table: "SystemMessage",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "vi",
                table: "SystemMessage",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "en",
                table: "SystemMessage",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "SystemMessage",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "SystemMessage",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "SystemMessage",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "SystemMessage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "SystemMessage",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "Snake",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "common_name",
                table: "Snake",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "Snake",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "Snake",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "Snake",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "habitat",
                table: "Snake",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "Snake",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "toxicity_level",
                table: "Snake",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "Snake",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "Snake",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_User_Id",
                table: "User",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SystemMessage_Id",
                table: "SystemMessage",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Snake_Id",
                table: "Snake",
                column: "id");

            migrationBuilder.CreateTable(
                name: "ContentPost",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    body_content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    thumbnail_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    author_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    is_published = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPost_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_ContentPost_User_AuthorId",
                        column: x => x.author_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirstAidDetail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    step_order = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    content_markdown = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    image_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    language_code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirstAidDetail_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_FirstAidDetail_Snake_SnakeId",
                        column: x => x.snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incident",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    victim_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    location = table.Column<Point>(type: "geography", nullable: false),
                    address_string = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    current_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    priority_level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ai_prediction_result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ai_confidence_score = table.Column<double>(type: "float", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incident_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_Incident_Snake_SnakeId",
                        column: x => x.snake_id,
                        principalTable: "Snake",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Incident_User_VictimId",
                        column: x => x.victim_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalFacility",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    location = table.Column<Point>(type: "geography", nullable: false),
                    phone_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    operating_hours = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalFacility_Id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLog_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_NotificationLog_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointTransaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    amount = table.Column<int>(type: "int", nullable: false),
                    activity_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    reference_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransaction_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_PointTransaction_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quiz",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    difficulty_level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    points_reward = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quiz_Id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "RescuerProfile",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    experience_years = table.Column<int>(type: "int", nullable: false),
                    vehicle_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    license_plate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    coverage_radius_km = table.Column<double>(type: "float", nullable: false),
                    is_verified = table.Column<bool>(type: "bit", nullable: false),
                    approved_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescuerProfile_UserId", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_RescuerProfile_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role_Id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SnakeHotspot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    location = table.Column<Point>(type: "geography", nullable: false),
                    snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    risk_level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    upvotes = table.Column<int>(type: "int", nullable: false),
                    verified_by_expert = table.Column<bool>(type: "bit", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnakeHotspot_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_SnakeHotspot_Snake_SnakeId",
                        column: x => x.snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SnakeHotspot_User_ReporterId",
                        column: x => x.reporter_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SnakeImage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    snake_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    is_primary = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnakeImage_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_SnakeImage_Snake_SnakeId",
                        column: x => x.snake_id,
                        principalTable: "Snake",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfig",
                columns: table => new
                {
                    config_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfig_ConfigKey", x => x.config_key);
                });

            migrationBuilder.CreateTable(
                name: "UserDevice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevice_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserDevice_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginHistory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    login_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginHistory_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserLoginHistory_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPoint",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    current_points = table.Column<int>(type: "int", nullable: false),
                    lifetime_points = table.Column<int>(type: "int", nullable: false),
                    current_rank = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPoint_UserId", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_UserPoint_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentMedia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    incident_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    media_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    media_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentMedia_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_IncidentMedia_Incident_IncidentId",
                        column: x => x.incident_id,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentStatusHistory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    incident_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status_from = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    status_to = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    change_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentStatusHistory_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_IncidentStatusHistory_Incident_IncidentId",
                        column: x => x.incident_id,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RescueMission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    incident_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rescuer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    arrived_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rescuer_notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    patient_condition_at_handover = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescueMission_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_RescueMission_Incident_IncidentId",
                        column: x => x.incident_id,
                        principalTable: "Incident",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RescueMission_User_RescuerId",
                        column: x => x.rescuer_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole_UserId_RoleId", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.role_id,
                        principalTable: "Role",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "RescueTrackingLog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    mission_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rescuer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    location = table.Column<Point>(type: "geography", nullable: false),
                    speed_kmh = table.Column<double>(type: "float", nullable: true),
                    accuracy_meters = table.Column<double>(type: "float", nullable: true),
                    battery_level = table.Column<int>(type: "int", nullable: true),
                    logged_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescueTrackingLog_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_RescueTrackingLog_RescueMission_MissionId",
                        column: x => x.mission_id,
                        principalTable: "RescueMission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    mission_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rating = table.Column<int>(type: "int", nullable: false),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Review_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_Review_RescueMission_MissionId",
                        column: x => x.mission_id,
                        principalTable: "RescueMission",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Review_User_ReviewerId",
                        column: x => x.reviewer_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Review_User_TargetId",
                        column: x => x.target_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    mission_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    payment_gateway = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    gateway_transaction_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction_Id", x => x.id);
                    table.ForeignKey(
                        name: "FK_Transaction_RescueMission_MissionId",
                        column: x => x.mission_id,
                        principalTable: "RescueMission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transaction_User_UserId",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPost_author_id",
                table: "ContentPost",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPost_Slug",
                table: "ContentPost",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirstAidDetail_snake_id",
                table: "FirstAidDetail",
                column: "snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Code",
                table: "Incident",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incident_snake_id",
                table: "Incident",
                column: "snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_victim_id",
                table: "Incident",
                column: "victim_id");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentMedia_incident_id",
                table: "IncidentMedia",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentStatusHistory_incident_id",
                table: "IncidentStatusHistory",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_user_id",
                table: "NotificationLog",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransaction_user_id",
                table: "PointTransaction",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_RescueMission_incident_id",
                table: "RescueMission",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_RescueMission_rescuer_id",
                table: "RescueMission",
                column: "rescuer_id");

            migrationBuilder.CreateIndex(
                name: "IX_RescueTrackingLog_mission_id",
                table: "RescueTrackingLog",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_Review_mission_id",
                table: "Review",
                column: "mission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Review_reviewer_id",
                table: "Review",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_Review_target_id",
                table: "Review",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "IX_Role_RoleName",
                table: "Role",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SnakeHotspot_reporter_id",
                table: "SnakeHotspot",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_SnakeHotspot_snake_id",
                table: "SnakeHotspot",
                column: "snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_SnakeImage_snake_id",
                table: "SnakeImage",
                column: "snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_mission_id",
                table: "Transaction",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_user_id",
                table: "Transaction",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevice_user_id",
                table: "UserDevice",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginHistory_user_id",
                table: "UserLoginHistory",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_role_id",
                table: "UserRole",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshToken_UserId",
                table: "RefreshToken",
                column: "user_id",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshToken_UserId",
                table: "RefreshToken");

            migrationBuilder.DropTable(
                name: "ContentPost");

            migrationBuilder.DropTable(
                name: "FirstAidDetail");

            migrationBuilder.DropTable(
                name: "IncidentMedia");

            migrationBuilder.DropTable(
                name: "IncidentStatusHistory");

            migrationBuilder.DropTable(
                name: "MedicalFacility");

            migrationBuilder.DropTable(
                name: "NotificationLog");

            migrationBuilder.DropTable(
                name: "PointTransaction");

            migrationBuilder.DropTable(
                name: "Quiz");

            migrationBuilder.DropTable(
                name: "RescuerProfile");

            migrationBuilder.DropTable(
                name: "RescueTrackingLog");

            migrationBuilder.DropTable(
                name: "Review");

            migrationBuilder.DropTable(
                name: "SnakeHotspot");

            migrationBuilder.DropTable(
                name: "SnakeImage");

            migrationBuilder.DropTable(
                name: "SystemConfig");

            migrationBuilder.DropTable(
                name: "Transaction");

            migrationBuilder.DropTable(
                name: "UserDevice");

            migrationBuilder.DropTable(
                name: "UserLoginHistory");

            migrationBuilder.DropTable(
                name: "UserPoint");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "RescueMission");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "Incident");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User_Id",
                table: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SystemMessage_Id",
                table: "SystemMessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Snake_Id",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "id",
                table: "User");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "User");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "User");

            migrationBuilder.DropColumn(
                name: "current_location",
                table: "User");

            migrationBuilder.DropColumn(
                name: "is_online",
                table: "User");

            migrationBuilder.DropColumn(
                name: "last_active_at",
                table: "User");

            migrationBuilder.DropColumn(
                name: "status",
                table: "User");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "User");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "User");

            migrationBuilder.DropColumn(
                name: "id",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "SystemMessage");

            migrationBuilder.DropColumn(
                name: "id",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "common_name",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "description",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "habitat",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "toxicity_level",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "Snake");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "Snake");

            migrationBuilder.RenameColumn(
                name: "vi",
                table: "SystemMessage",
                newName: "vietnamese_message");

            migrationBuilder.RenameColumn(
                name: "en",
                table: "SystemMessage",
                newName: "english_message");

            migrationBuilder.RenameColumn(
                name: "scientific_name",
                table: "Snake",
                newName: "name");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "User",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "User",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "User",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "dob",
                table: "User",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "avatar",
                table: "User",
                type: "varchar(2048)",
                unicode: false,
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "User",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newsequentialid())");

            migrationBuilder.AddColumn<DateTime>(
                name: "create_date",
                table: "User",
                type: "datetime",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "email_confirmed",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "email_verification_code",
                table: "User",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "User",
                type: "nvarchar(155)",
                maxLength: 155,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_date",
                table: "User",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "phone_number_confirmed",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "phone_verification_code",
                table: "User",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "phone_verification_expiry",
                table: "User",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "role_id",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_backup_codes",
                table: "User",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret_key",
                table: "User",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "msg_id",
                table: "SystemMessage",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "msg_content",
                table: "SystemMessage",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "vietnamese_message",
                table: "SystemMessage",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "english_message",
                table: "SystemMessage",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "create_by",
                table: "SystemMessage",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "create_date",
                table: "SystemMessage",
                type: "datetime",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "SystemMessage",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_date",
                table: "SystemMessage",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "snake_id",
                table: "Snake",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User_UserId",
                table: "User",
                column: "user_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SystemMessage_MsgId",
                table: "SystemMessage",
                column: "msg_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Snake_SnakeId",
                table: "Snake",
                column: "snake_id");

            migrationBuilder.CreateIndex(
                name: "IX_Snake_Name",
                table: "Snake",
                column: "name");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshToken_UserId",
                table: "RefreshToken",
                column: "user_id",
                principalTable: "User",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}