-- One-time setup: the four VMaNGOS databases and the account the servers
-- connect with (matches the *.Info defaults in mangosd.conf / realmd.conf).
CREATE DATABASE IF NOT EXISTS realmd DEFAULT CHARSET utf8 COLLATE utf8_general_ci;
CREATE DATABASE IF NOT EXISTS characters DEFAULT CHARSET utf8 COLLATE utf8_general_ci;
CREATE DATABASE IF NOT EXISTS mangos DEFAULT CHARSET utf8 COLLATE utf8_general_ci;
CREATE DATABASE IF NOT EXISTS logs DEFAULT CHARSET utf8 COLLATE utf8_general_ci;

CREATE USER IF NOT EXISTS 'mangos'@'localhost' IDENTIFIED BY 'mangos';
CREATE USER IF NOT EXISTS 'mangos'@'127.0.0.1' IDENTIFIED BY 'mangos';
GRANT ALL PRIVILEGES ON realmd.* TO 'mangos'@'localhost', 'mangos'@'127.0.0.1';
GRANT ALL PRIVILEGES ON characters.* TO 'mangos'@'localhost', 'mangos'@'127.0.0.1';
GRANT ALL PRIVILEGES ON mangos.* TO 'mangos'@'localhost', 'mangos'@'127.0.0.1';
GRANT ALL PRIVILEGES ON logs.* TO 'mangos'@'localhost', 'mangos'@'127.0.0.1';
FLUSH PRIVILEGES;
