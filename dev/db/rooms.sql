-- ルーム

CREATE TABLE rooms(
	room_id VARCHAR(32) PRIMARY KEY,
	explanation VARCHAR(100) NULL,
	pw VARCHAR(16) NULL,
	rgdt DATETIME DEFAULT CURRENT_TIMESTAMP,
	updt DATETIME DEFAULT CURRENT_TIMESTAMP
);

