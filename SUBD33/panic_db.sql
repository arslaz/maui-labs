-- ==============================================================================
-- Лабораторная работа №33: СИСТЕМЫ УПРАВЛЕНИЯ БАЗАМИ ДАННЫХ
-- Вариант 9: Нервные срывы в сессию
-- Группа: ПО2311, 4 курс
-- Студенты: 
--   1. Зубрицкий Игорь Николаевич
--   2. Лазаревич Арсений Александрович
--   3. Шаповалов Анатолий Сергеевич
--   4. Малащицкий Алексей Николаевич
--   5. Ковалёв Никита Сергеевич
-- База данных: panic_db (MS SQL Server)
-- ==============================================================================

-- 1. СОЗДАНИЕ БАЗЫ ДАННЫХ
IF DB_ID('panic_db') IS NULL
BEGIN
    CREATE DATABASE panic_db;
END
GO

USE panic_db;
GO

-- Удаление таблиц при повторном запуске скрипта
IF OBJECT_ID('Incidents', 'U') IS NOT NULL DROP TABLE Incidents;
IF OBJECT_ID('Triggers', 'U') IS NOT NULL DROP TABLE Triggers;
IF OBJECT_ID('Students', 'U') IS NOT NULL DROP TABLE Students;
GO

-- ==============================================================================
-- 2. СОЗДАНИЕ ТАБЛИЦ И ВНЕШНИХ КЛЮЧЕЙ (Задание 1)
-- ==============================================================================

-- 1. Студенты: id, ФИО, группа, курс, фото
CREATE TABLE Students (
    id INT IDENTITY(1,1) PRIMARY KEY,
    full_name NVARCHAR(100) NOT NULL,
    group_name NVARCHAR(50) NOT NULL,
    course INT NOT NULL CHECK (course BETWEEN 1 AND 6),
    photo NVARCHAR(255) NULL
);
GO

-- 2. Триггеры: id, название
CREATE TABLE Triggers (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(150) NOT NULL
);
GO

-- 3. Инциденты: id, студент (FK), дата, триггер (FK), уровень паники (1-10), способ успокоения, помогло
CREATE TABLE Incidents (
    id INT IDENTITY(1,1) PRIMARY KEY,
    student_id INT NOT NULL,
    incident_date DATETIME NOT NULL,
    trigger_id INT NOT NULL,
    panic_level INT NOT NULL CHECK (panic_level BETWEEN 1 AND 10),
    calming_method NVARCHAR(200) NOT NULL,
    helped BIT NOT NULL, -- 1 = Да, 0 = Нет
    CONSTRAINT FK_Incidents_Students FOREIGN KEY (student_id) REFERENCES Students(id) ON DELETE CASCADE,
    CONSTRAINT FK_Incidents_Triggers FOREIGN KEY (trigger_id) REFERENCES Triggers(id) ON DELETE CASCADE
);
GO

-- ==============================================================================
-- 3. ЗАПОЛНЕНИЕ ТЕСТОВЫМИ ДАННЫМИ (Задание 1)
-- ==============================================================================

-- Справочник триггеров (5 записей по условию)
INSERT INTO Triggers (name) VALUES 
('Увидел расписание'),
('Получил двойку'),
('Интернет пропал'),
('Не работает IDE'),
('Кончился кофе');
GO

-- Справочник студентов (Группа ПО2311, 4 курс, 5 записей)
INSERT INTO Students (full_name, group_name, course, photo) VALUES 
('Зубрицкий Игорь Николаевич', 'ПО2311', 4, 'student1.png'),
('Лазаревич Арсений Александрович', 'ПО2311', 4, 'student2.png'),
('Шаповалов Анатолий Сергеевич', 'ПО2311', 4, 'student3.png'),
('Малащицкий Алексей Николаевич', 'ПО2311', 4, 'student4.png'),
('Ковалёв Никита Сергеевич', 'ПО2311', 4, 'student5.png');
GO

-- Основная таблица инцидентов (12 записей, минимум 10 по условию)
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

-- ==============================================================================
-- 4. ЗАПРОСЫ ДЛЯ ЗАДАНИЯ 5 (MS SQL Server)
-- ==============================================================================

-- 1. Топ-3 триггера по количеству инцидентов
SELECT TOP 3 
    t.name AS [Триггер], 
    COUNT(i.id) AS [Количество инцидентов]
FROM Triggers t
JOIN Incidents i ON t.id = i.trigger_id
GROUP BY t.id, t.name
ORDER BY [Количество инцидентов] DESC;
GO

-- 2. Студент с наибольшим средним уровнем паники
SELECT TOP 1 
    s.full_name AS [Студент], 
    s.group_name AS [Группа], 
    ROUND(AVG(CAST(i.panic_level AS FLOAT)), 2) AS [Средний уровень паники]
FROM Students s
JOIN Incidents i ON s.id = i.student_id
GROUP BY s.id, s.full_name, s.group_name
ORDER BY [Средний уровень паники] DESC;
GO

-- 3. Самый эффективный способ успокоения (помогло = да)
SELECT TOP 1 
    calming_method AS [Способ успокоения], 
    COUNT(*) AS [Успешных применений (помогло = да)]
FROM Incidents
WHERE helped = 1
GROUP BY calming_method
ORDER BY [Успешных применений (помогло = да)] DESC;
GO

-- 4. Инциденты с уровнем паники 9 или 10
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

-- 5. Количество инцидентов по каждому студенту
SELECT 
    s.full_name AS [Студент], 
    s.group_name AS [Группа], 
    COUNT(i.id) AS [Количество инцидентов]
FROM Students s
LEFT JOIN Incidents i ON s.id = i.student_id
GROUP BY s.id, s.full_name, s.group_name
ORDER BY [Количество инцидентов] DESC, s.full_name ASC;
GO

-- 6. Триггер с наибольшим средним уровнем паники
SELECT TOP 1 
    t.name AS [Триггер], 
    ROUND(AVG(CAST(i.panic_level AS FLOAT)), 2) AS [Средний уровень паники],
    COUNT(i.id) AS [Всего инцидентов]
FROM Triggers t
JOIN Incidents i ON t.id = i.trigger_id
GROUP BY t.id, t.name
ORDER BY [Средний уровень паники] DESC;
GO

-- 7. Процент случаев когда способ успокоения помог
SELECT 
    COUNT(*) AS [Всего инцидентов],
    SUM(CASE WHEN helped = 1 THEN 1 ELSE 0 END) AS [Успешно помогло],
    CONVERT(VARCHAR(10), ROUND(CAST(SUM(CASE WHEN helped = 1 THEN 1 ELSE 0 END) AS FLOAT) * 100.0 / COUNT(*), 2)) + '%' AS [Процент успеха]
FROM Incidents;
GO
