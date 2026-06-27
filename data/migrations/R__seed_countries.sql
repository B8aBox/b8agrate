INSERT INTO dbo.Country (Code, Name)
SELECT source.Code, source.Name
FROM (VALUES
    ('US', N'United States'),
    ('CA', N'Canada'),
    ('MX', N'Mexico')
) AS source (Code, Name)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Country target
    WHERE target.Code = source.Code
);
GO
