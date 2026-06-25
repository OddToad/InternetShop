-- Проверяем существование и создаем базу данных для Keycloak
SELECT 'CREATE DATABASE keycloak_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak_db')\gexec

-- Проверяем существование и создаем базу данных для тестового микросервиса
SELECT 'CREATE DATABASE internetshop_test_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'internetshop_test_db')\gexec