
CREATE PROCEDURE set_mail_token
@pre_user_id VARCHAR(254)
AS
BEGIN


DECLARE @is_exist BIT = 0;


SELECT @is_exist = 1
WHERE EXISTS(
	SELECT pre_user_id
	FROM pre_users
	WHERE pre_user_id = @pre_user_id
);

INSERT INTO pre_users(pre_user_id)
SELECT @pre_user_id
WHERE @is_exist <> 0;

UPDATE pre_users
SET updt = CURRENT_TIMESTAMP
WHERE @is_exist = 1;


END
