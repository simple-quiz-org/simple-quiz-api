-- ルーム

CREATE TABLE rooms(
	room_id VARCHAR(32) PRIMARY KEY,
	room_name VARCHAR(30) NOT NULL,
	room_icon VARCHAR(38) NULL,
	explanation NVARCHAR(100) NULL,
	pw CHAR(4) NULL CHECK(LEN(pw) = 4),
	is_public BIT NOT NULL,
	is_valid BIT DEFAULT 1,
	rgdt DATETIME DEFAULT CURRENT_TIMESTAMP,
	updt DATETIME DEFAULT CURRENT_TIMESTAMP
);


