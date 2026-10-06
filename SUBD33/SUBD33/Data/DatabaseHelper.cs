using MySqlConnector;
using SUBD33.Models;

namespace SUBD33.Data;

public class DatabaseHelper
{
    private const string ConnectionString = "Server=localhost;Port=3306;Database=panic_db;User ID=root;Password=;AllowPublicKeyRetrieval=True;SSL Mode=None;";

    private static DatabaseHelper? _instance;
    public static DatabaseHelper Instance => _instance ??= new DatabaseHelper();

    public string DatabaseLocation => "MySQL (localhost:3306 / panic_db)";

    public DatabaseHelper()
    {
        InitializeDatabase();
    }

    private MySqlConnection CreateConnection()
    {
        var conn = new MySqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public void InitializeDatabase()
    {
        try
        {
            using (var serverConn = new MySqlConnection("Server=localhost;Port=3306;User ID=root;Password=;AllowPublicKeyRetrieval=True;SSL Mode=None;"))
            {
                serverConn.Open();
                using var createDbCmd = new MySqlCommand("CREATE DATABASE IF NOT EXISTS panic_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", serverConn);
                createDbCmd.ExecuteNonQuery();
            }

            using var conn = CreateConnection();

            string createStudentsSql = @"
                CREATE TABLE IF NOT EXISTS Students (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    full_name VARCHAR(100) NOT NULL,
                    group_name VARCHAR(50) NOT NULL,
                    course INT NOT NULL,
                    photo VARCHAR(255) NULL
                );";
            using (var cmd = new MySqlCommand(createStudentsSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            string createTriggersSql = @"
                CREATE TABLE IF NOT EXISTS Triggers (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(150) NOT NULL
                );";
            using (var cmd = new MySqlCommand(createTriggersSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            string createIncidentsSql = @"
                CREATE TABLE IF NOT EXISTS Incidents (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    student_id INT NOT NULL,
                    incident_date DATETIME NOT NULL,
                    trigger_id INT NOT NULL,
                    panic_level INT NOT NULL,
                    calming_method VARCHAR(200) NOT NULL,
                    helped TINYINT(1) NOT NULL,
                    CONSTRAINT FK_Incidents_Students FOREIGN KEY (student_id) REFERENCES Students(id) ON DELETE CASCADE,
                    CONSTRAINT FK_Incidents_Triggers FOREIGN KEY (trigger_id) REFERENCES Triggers(id) ON DELETE CASCADE
                );";
            using (var cmd = new MySqlCommand(createIncidentsSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            string checkSql = "SELECT COUNT(*) FROM Students WHERE full_name LIKE '%Зубрицкий%';";
            using (var checkCmd = new MySqlCommand(checkSql, conn))
            {
                long count = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0L);
                if (count == 0)
                {
                    using var delCmd = new MySqlCommand("DELETE FROM Incidents; DELETE FROM Students; DELETE FROM Triggers;", conn);
                    delCmd.ExecuteNonQuery();
                    SeedData(conn);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseHelper Init Error]: {ex.Message}");
        }
    }

    private void SeedData(MySqlConnection conn)
    {
        using var transaction = conn.BeginTransaction();
        try
        {
            string insertTriggers = @"
                INSERT INTO Triggers (name) VALUES 
                ('Увидел расписание'),
                ('Получил двойку'),
                ('Интернет пропал'),
                ('Не работает IDE'),
                ('Кончился кофе');";
            using (var cmd = new MySqlCommand(insertTriggers, conn, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            string insertStudents = @"
                INSERT INTO Students (full_name, group_name, course, photo) VALUES 
                ('Зубрицкий Игорь Николаевич', 'ПО2311', 4, 'student1.png'),
                ('Лазаревич Арсений Александрович', 'ПО2311', 4, 'student2.png'),
                ('Шаповалов Анатолий Сергеевич', 'ПО2311', 4, 'student3.png'),
                ('Малащицкий Алексей Николаевич', 'ПО2311', 4, 'student4.png'),
                ('Ковалёв Никита Сергеевич', 'ПО2311', 4, 'student5.png');";
            using (var cmd = new MySqlCommand(insertStudents, conn, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            string insertIncidents = @"
                INSERT INTO Incidents (student_id, incident_date, trigger_id, panic_level, calming_method, helped) VALUES 
                (1, '2026-06-10 23:58:00', 1, 10, 'Выпил валерьянку', 1),
                (1, '2026-06-12 14:00:00', 4, 8, 'Перезагрузил Rider', 1),
                (2, '2026-06-11 10:30:00', 2, 9, 'Пошёл пить чай', 0),
                (2, '2026-06-15 12:00:00', 5, 7, 'Сходил в магазин', 1),
                (3, '2026-06-08 19:00:00', 3, 8, 'Раздал мобильный интернет', 1),
                (3, '2026-06-14 23:58:00', 1, 9, 'Глубокое дыхание', 0),
                (4, '2026-06-09 13:30:00', 4, 10, 'Удалил bin и obj', 1),
                (4, '2026-06-13 17:00:00', 5, 6, 'Заварил цикорий', 0),
                (5, '2026-06-07 20:00:00', 1, 10, 'Лёг спать', 1),
                (5, '2026-06-12 11:00:00', 2, 8, 'Пошёл на пересдачу', 1),
                (2, '2026-06-18 21:00:00', 3, 7, 'Раздал мобильный интернет', 1),
                (1, '2026-06-20 23:58:00', 1, 10, 'Выпил валерьянку', 1);";
            using (var cmd = new MySqlCommand(insertIncidents, conn, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<Incident> GetIncidents(string? searchQuery = null, string sortBy = "Дата", bool ascending = true)
    {
        var list = new List<Incident>();
        using var conn = CreateConnection();

        string sql = @"
            SELECT 
                i.id, 
                i.student_id, 
                s.full_name, 
                s.group_name, 
                s.course, 
                s.photo,
                i.incident_date, 
                i.trigger_id, 
                t.name AS trigger_name, 
                i.panic_level, 
                i.calming_method, 
                i.helped
            FROM Incidents i
            JOIN Students s ON i.student_id = s.id
            JOIN Triggers t ON i.trigger_id = t.id";

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            sql += @" WHERE s.full_name LIKE @search 
                      OR t.name LIKE @search 
                      OR i.calming_method LIKE @search 
                      OR s.group_name LIKE @search";
        }

        string orderColumn = sortBy switch
        {
            "Уровень паники" => "i.panic_level",
            "Студент" => "s.full_name",
            "Триггер" => "t.name",
            _ => "i.incident_date"
        };

        sql += $" ORDER BY {orderColumn} {(ascending ? "ASC" : "DESC")};";

        using var cmd = new MySqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            cmd.Parameters.AddWithValue("@search", $"%{searchQuery.Trim()}%");
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var incident = new Incident
            {
                Id = reader.GetInt32(0),
                StudentId = reader.GetInt32(1),
                StudentName = reader.GetString(2),
                GroupName = reader.GetString(3),
                Course = reader.GetInt32(4),
                StudentPhoto = reader.IsDBNull(5) ? "" : reader.GetString(5),
                IncidentDate = reader.GetDateTime(6),
                TriggerId = reader.GetInt32(7),
                TriggerName = reader.GetString(8),
                PanicLevel = reader.GetInt32(9),
                CalmingMethod = reader.GetString(10),
                Helped = reader.GetBoolean(11)
            };
            list.Add(incident);
        }

        return list;
    }

    public bool AddIncident(int studentId, DateTime date, int triggerId, int panicLevel, string calmingMethod, bool helped)
    {
        using var conn = CreateConnection();
        string sql = @"
            INSERT INTO Incidents (student_id, incident_date, trigger_id, panic_level, calming_method, helped)
            VALUES (@studentId, @date, @triggerId, @panicLevel, @calmingMethod, @helped);";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@studentId", studentId);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@triggerId", triggerId);
        cmd.Parameters.AddWithValue("@panicLevel", panicLevel);
        cmd.Parameters.AddWithValue("@calmingMethod", calmingMethod);
        cmd.Parameters.AddWithValue("@helped", helped ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteIncident(int id)
    {
        using var conn = CreateConnection();
        string sql = "DELETE FROM Incidents WHERE id = @id;";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<Student> GetStudents()
    {
        var list = new List<Student>();
        using var conn = CreateConnection();
        string sql = "SELECT id, full_name, group_name, course, photo FROM Students ORDER BY id;";
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Student
            {
                Id = reader.GetInt32(0),
                FullName = reader.GetString(1),
                GroupName = reader.GetString(2),
                Course = reader.GetInt32(3),
                Photo = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return list;
    }

    public bool AddStudent(string fullName, string group, int course, string photoPath)
    {
        using var conn = CreateConnection();
        string sql = @"
            INSERT INTO Students (full_name, group_name, course, photo)
            VALUES (@name, @group, @course, @photo);";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", fullName);
        cmd.Parameters.AddWithValue("@group", group);
        cmd.Parameters.AddWithValue("@course", course);
        cmd.Parameters.AddWithValue("@photo", photoPath ?? string.Empty);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<TriggerItem> GetTriggers()
    {
        var list = new List<TriggerItem>();
        using var conn = CreateConnection();
        string sql = "SELECT id, name FROM Triggers ORDER BY id;";
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TriggerItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return list;
    }

    public StudentCard? GetStudentCard(int studentId)
    {
        using var conn = CreateConnection();

        string studentSql = "SELECT id, full_name, group_name, course, photo FROM Students WHERE id = @id;";
        Student? student = null;
        using (var cmd = new MySqlCommand(studentSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", studentId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                student = new Student
                {
                    Id = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    GroupName = reader.GetString(2),
                    Course = reader.GetInt32(3),
                    Photo = reader.IsDBNull(4) ? "" : reader.GetString(4)
                };
            }
        }

        if (student == null) return null;

        var card = new StudentCard
        {
            StudentId = student.Id,
            FullName = student.FullName,
            GroupName = student.GroupName,
            Course = student.Course,
            Photo = student.Photo
        };

        string statsSql = @"
            SELECT COUNT(id), IFNULL(AVG(panic_level), 0.0) 
            FROM Incidents 
            WHERE student_id = @id;";
        using (var cmd = new MySqlCommand(statsSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", studentId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                card.IncidentCount = reader.GetInt32(0);
                card.AveragePanicLevel = Math.Round(reader.GetDouble(1), 2);
            }
        }

        string topTriggerSql = @"
            SELECT t.name, COUNT(i.id) AS cnt
            FROM Incidents i
            JOIN Triggers t ON i.trigger_id = t.id
            WHERE i.student_id = @id
            GROUP BY t.id, t.name
            ORDER BY cnt DESC
            LIMIT 1;";
        using (var cmd = new MySqlCommand(topTriggerSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", studentId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                card.MostFrequentTrigger = reader.GetString(0);
            }
            else
            {
                card.MostFrequentTrigger = "Нет инцидентов";
            }
        }

        return card;
    }

    public List<Incident> GetStudentIncidents(int studentId)
    {
        return GetIncidents().Where(i => i.StudentId == studentId).ToList();
    }

    public QueryResult ExecuteLabQuery(int queryNumber)
    {
        string title;
        string sql;

        switch (queryNumber)
        {
            case 1:
                title = "1. Топ-3 триггера по количеству инцидентов";
                sql = @"
                    SELECT 
                        t.name AS `Триггер`, 
                        COUNT(i.id) AS `Количество инцидентов`
                    FROM Triggers t
                    JOIN Incidents i ON t.id = i.trigger_id
                    GROUP BY t.id, t.name
                    ORDER BY `Количество инцидентов` DESC
                    LIMIT 3;";
                break;

            case 2:
                title = "2. Студент с наибольшим средним уровнем паники";
                sql = @"
                    SELECT 
                        s.full_name AS `Студент`, 
                        s.group_name AS `Группа`, 
                        ROUND(AVG(i.panic_level), 2) AS `Средний уровень паники`
                    FROM Students s
                    JOIN Incidents i ON s.id = i.student_id
                    GROUP BY s.id, s.full_name, s.group_name
                    ORDER BY `Средний уровень паники` DESC
                    LIMIT 1;";
                break;

            case 3:
                title = "3. Самый эффективный способ успокоения (помогло = да)";
                sql = @"
                    SELECT 
                        calming_method AS `Способ успокоения`, 
                        COUNT(*) AS `Успешных применений (помогло = да)`
                    FROM Incidents
                    WHERE helped = 1
                    GROUP BY calming_method
                    ORDER BY `Успешных применений (помогло = да)` DESC
                    LIMIT 1;";
                break;

            case 4:
                title = "4. Инциденты с уровнем паники 9 или 10";
                sql = @"
                    SELECT 
                        i.id AS `№`, 
                        s.full_name AS `Студент`, 
                        DATE_FORMAT(i.incident_date, '%d.%m.%Y %H:%i') AS `Дата инцидента`, 
                        t.name AS `Триггер`, 
                        i.panic_level AS `Уровень паники`, 
                        i.calming_method AS `Способ успокоения`,
                        CASE WHEN i.helped = 1 THEN 'Да' ELSE 'Нет' END AS `Помогло`
                    FROM Incidents i
                    JOIN Students s ON i.student_id = s.id
                    JOIN Triggers t ON i.trigger_id = t.id
                    WHERE i.panic_level >= 9
                    ORDER BY i.panic_level DESC, i.incident_date DESC;";
                break;

            case 5:
                title = "5. Количество инцидентов по каждому студенту";
                sql = @"
                    SELECT 
                        s.full_name AS `Студент`, 
                        s.group_name AS `Группа`, 
                        COUNT(i.id) AS `Количество инцидентов`
                    FROM Students s
                    LEFT JOIN Incidents i ON s.id = i.student_id
                    GROUP BY s.id, s.full_name, s.group_name
                    ORDER BY `Количество инцидентов` DESC, s.full_name ASC;";
                break;

            case 6:
                title = "6. Триггер с наибольшим средним уровнем паники";
                sql = @"
                    SELECT 
                        t.name AS `Триггер`, 
                        ROUND(AVG(i.panic_level), 2) AS `Средний уровень паники`,
                        COUNT(i.id) AS `Всего инцидентов`
                    FROM Triggers t
                    JOIN Incidents i ON t.id = i.trigger_id
                    GROUP BY t.id, t.name
                    ORDER BY `Средний уровень паники` DESC
                    LIMIT 1;";
                break;

            case 7:
                title = "7. Процент случаев когда способ успокоения помог";
                sql = @"
                    SELECT 
                        COUNT(*) AS `Всего инцидентов`,
                        SUM(CASE WHEN helped = 1 THEN 1 ELSE 0 END) AS `Успешно помогло`,
                        CONCAT(ROUND(SUM(CASE WHEN helped = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2), '%') AS `Процент успеха`
                    FROM Incidents;";
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(queryNumber), "Номер запроса должен быть от 1 до 7.");
        }

        var result = new QueryResult
        {
            Title = title,
            SqlQuery = sql.Trim()
        };

        using var conn = CreateConnection();
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            result.Headers.Add(reader.GetName(i));
        }

        while (reader.Read())
        {
            var row = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row.Add(reader.IsDBNull(i) ? "-" : reader.GetValue(i).ToString() ?? "-");
            }
            result.Rows.Add(row);
        }

        return result;
    }
}
