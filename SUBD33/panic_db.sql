IF DB_ID('panic_db') IS NULL
BEGIN
    CREATE DATABASE panic_db;
END
GO

USE panic_db;
GO

IF OBJECT_ID('Incidents', 'U') IS NOT NULL DROP TABLE Incidents;
IF OBJECT_ID('Triggers', 'U') IS NOT NULL DROP TABLE Triggers;
IF OBJECT_ID('Students', 'U') IS NOT NULL DROP TABLE Students;
GO

CREATE TABLE Students (
    id INT IDENTITY(1,1) PRIMARY KEY,
    full_name NVARCHAR(100) NOT NULL,
    group_name NVARCHAR(50) NOT NULL,
    course INT NOT NULL CHECK (course BETWEEN 1 AND 6),
    photo NVARCHAR(255) NULL
);
GO

CREATE TABLE Triggers (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(150) NOT NULL
);
GO

CREATE TABLE Incidents (
    id INT IDENTITY(1,1) PRIMARY KEY,
    student_id INT NOT NULL,
    incident_date DATETIME NOT NULL,
    trigger_id INT NOT NULL,
    panic_level INT NOT NULL CHECK (panic_level BETWEEN 1 AND 10),
    calming_method NVARCHAR(200) NOT NULL,
    helped BIT NOT NULL,
    CONSTRAINT FK_Incidents_Students FOREIGN KEY (student_id) REFERENCES Students(id) ON DELETE CASCADE,
    CONSTRAINT FK_Incidents_Triggers FOREIGN KEY (trigger_id) REFERENCES Triggers(id) ON DELETE CASCADE
);
GO

INSERT INTO Triggers (name) VALUES 
('Увидел расписание'),
('Получил двойку'),
('Интернет пропал'),
('Не работает IDE'),
('Кончился кофе');
GO

INSERT INTO Students (full_name, group_name, course, photo) VALUES 
('Зубрицкий Игорь Николаевич', 'ПО2311', 4, 'student1.png'),
('Лазаревич Арсений Александрович', 'ПО2311', 4, 'student2.png'),
('Шаповалов Анатолий Сергеевич', 'ПО2311', 4, 'student3.png'),
('Малащицкий Алексей Николаевич', 'ПО2311', 4, 'student4.png'),
('Ковалёв Никита Сергеевич', 'ПО2311', 4, 'student5.png');
GO

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
(1, '2026-06-20 23:58:00', 1, 10, 'Выпил валерьянку', 1);
GO

SELECT TOP 3 
    t.name AS [Триггер], 
    COUNT(i.id) AS [Количество инцидентов]
FROM Triggers t
JOIN Incidents i ON t.id = i.trigger_id
GROUP BY t.id, t.name
ORDER BY [Количество инцидентов] DESC;
GO

SELECT TOP 1 
    s.full_name AS [Студент], 
    s.group_name AS [Группа], 
    ROUND(AVG(CAST(i.panic_level AS FLOAT)), 2) AS [Средний уровень паники]
FROM Students s
JOIN Incidents i ON s.id = i.student_id
GROUP BY s.id, s.full_name, s.group_name
ORDER BY [Средний уровень паники] DESC;
GO

SELECT TOP 1 
    calming_method AS [Способ успокоения], 
    COUNT(*) AS [Успешных применений (помогло = да)]
FROM Incidents
WHERE helped = 1
GROUP BY calming_method
ORDER BY [Успешных применений (помогло = да)] DESC;
GO

SELECT 
    i.id AS [№], 
    s.full_name AS [Студент], 
    i.incident_date AS [Дата инцидента], 
    t.name AS [Триггер], 
    i.panic_level AS [Уровень паники], 
    i.calming_method AS [Способ успокоения],
    CASE WHEN i.helped = 1 THEN 'Да' ELSE 'Нет' END AS [Помогло]
FROM Incidents i
JOIN Students s ON i.student_id = s.id
JOIN Triggers t ON i.trigger_id = t.id
WHERE i.panic_level >= 9
ORDER BY i.panic_level DESC, i.incident_date DESC;
GO

SELECT 
    s.full_name AS [Студент], 
    s.group_name AS [Группа], 
    COUNT(i.id) AS [Количество инцидентов]
FROM Students s
LEFT JOIN Incidents i ON s.id = i.student_id
GROUP BY s.id, s.full_name, s.group_name
ORDER BY [Количество инцидентов] DESC, s.full_name ASC;
GO

SELECT TOP 1 
    t.name AS [Триггер], 
    ROUND(AVG(CAST(i.panic_level AS FLOAT)), 2) AS [Средний уровень паники],
    COUNT(i.id) AS [Всего инцидентов]
FROM Triggers t
JOIN Incidents i ON t.id = i.trigger_id
GROUP BY t.id, t.name
ORDER BY [Средний уровень паники] DESC;
GO

SELECT 
    COUNT(*) AS [Всего инцидентов],
    SUM(CASE WHEN helped = 1 THEN 1 ELSE 0 END) AS [Успешно помогло],
    CONVERT(VARCHAR(10), ROUND(CAST(SUM(CASE WHEN helped = 1 THEN 1 ELSE 0 END) AS FLOAT) * 100.0 / COUNT(*), 2)) + '%' AS [Процент успеха]
FROM Incidents;
GO
