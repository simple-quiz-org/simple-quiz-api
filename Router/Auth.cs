using DBMod;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using MailMod;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;


#pragma warning disable

public record SignUpStruct(
	string token
);

public record SignInStruct(
	string uid,
	string password
);


public record PreSignUpStruct(
	string mail,
	string user_id,
	string password,
	string user_name,
	string comment,
	string user_icon
);


#pragma warning restore

internal static class Auth
{


    /// <summary>
    /// 【完成】セッション管理用のトークンを生成します。
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Get /auth/session_id
	/// 	
	/// </remarks>
    /// <returns>
	/// Sample response:
	/// 
	/// 	{
	/// 		"token": "fba8c49f09f140d693ddf2a33491a82e"
	/// 	}
	/// 
    /// </returns>
	/// <response code="200">正常にトークンの生成処理が実行されました。</response>
	/// <response code="500">トークン生成中に例外が発生しました。</response>
    [HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static dynamic GenerateToken()
	{
		try {
			string session_id = Guid.NewGuid().ToString("N");

			DBClient client = new();
			client.Add("INSERT INTO sessions(session_id)");
			client.Add("VALUES(@session_id);");
			client.AddParam(session_id);
			client.SetDataType("@session_id", SqlDbType.VarChar);
			client.Execute();

			return Results.Ok(new {
				session_id = session_id,
			});
		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}

	}


    /// <summary>
    /// 【完成】指定したトークンがログイン情報を保有しているかを判定します。
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Get /auth/sign_in
	/// 	
	/// </remarks>
    /// <returns>
	/// 	{
	/// 		"is_login": true,
	/// 		"user_id": "hogehoge"
	/// 	}
	/// </returns>
	/// <response code="200">正常にログイン中かどうかを判定できました。</response>
	/// <response code="400">指定したトークンが不正です。</response>
	/// <response code="500">ログイン判定中に例外が発生しました。</response>
    [HttpGet]
	[Route("auth/is_login")]
	[Produces("application/json")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static IResult IsLogin([FromHeader(Name = "Authorization")] string session_id)
	{
		try
		{
			DBClient client = new();
			client.Add("SELECT user_id");
			client.Add("FROM sessions");
			client.Add("WHERE session_id = @session_id;");
			client.AddParam(session_id);
			client.SetDataType("@session_id", SqlDbType.VarChar);
			
			var result = client.Select();

			if (result == null)
			{
				return Results.BadRequest(new { message = "指定した認証トークンに不備があります。"});
			}

			if (result["user_id"]?.ToString() != null)
			{
				return Results.Ok(new {is_login = true, user_id = result["user_id"]?.ToString()});
			}
			else
			{
				return Results.Ok(new {is_login = false, user_id = ""});
			}
		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}


    /// <summary>
    /// 【完成】指定したメールアドレスを対象に仮会員登録処理を行います。
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Get /auth/pre_signup
	/// 	
	/// </remarks>
	/// <remarks>
	/// Sample request:
	///
	/// 	POST /auth/signup
	/// 	{
	/// 		"mail": "test@example.com"
	/// 	}
	///
	/// </remarks>
	/// <param name="preSignUpStruct"></param>
	/// <response code="200">正常に仮会員登録処理が完了しました。</response>
	/// <response code="400">不正なメールアドレスが指定されました。</response>
	/// <response code="500">会員登録処理中に例外が発生しました。</response>
	[Route("auth/pre_signup")]
	[Produces("application/json")]
	[HttpPost]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static IResult PreSignUp(PreSignUpStruct preSignUpStruct)
	{
		var user_id = preSignUpStruct.user_id;
		var mail = preSignUpStruct.mail;
		var user_name = preSignUpStruct.user_name;
		var comment = preSignUpStruct.comment ?? "";
		var password = preSignUpStruct.password;
		var user_icon = preSignUpStruct.user_icon;

		if (user_id == null) return Results.BadRequest(new {message = "ユーザIDを指定してください。"});
		if (mail == null) return Results.BadRequest(new {message = "メールアドレスを指定してください。"});
		if (user_name == null) return Results.BadRequest(new {message = "ユーザ名を指定してください。"});
		if (password == null) return Results.BadRequest(new {message = "パスワードを指定してください。"});

		// ユーザIDのチェック
		if (user_id.Length < 3 || 16 < user_id.Length)
		{
			return Results.BadRequest(new { message = "ユーザIDの長さが不正です。"});
		}
		// パスワードのチェック
		if (!Regex.IsMatch(password, @"^[a-zA-Z0-9-/:-@\[-\`\{-\~]+$"))
		{
			return Results.BadRequest(new { message = "パスワードは空白類似文字を除いた半角英数字のみで構成してください。"});
		}
		if (password.Length < 8 || 32 < password.Length)
		{
			return Results.BadRequest(new { message = "パスワードの文字数が不正です。"});
		}

		try
		{
			// メールアドレスのチェック
			if (!Regex.IsMatch(mail, @"^[a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$"))
			{
				return Results.BadRequest(new { message = "メールアドレスの形式が不正です。"});
			}
			if (254 < mail.Length)
			{
				return Results.BadRequest(new { message = "メールアドレスは254文字以内で入力してください。"});
			}

			DBClient client = new();

			// 既に登録済みかチェック
			// -> 対象は「pre_users」と「users」
			client.Add("SELECT user_id, mail");
			client.Add("FROM users");
			client.Add("WHERE user_id = @user_id OR mail = @mail");
			client.Add("UNION");
			client.Add("SELECT user_id, mail");
			client.Add("FROM pre_users");
			client.Add("WHERE user_id = @user_id;");
			client.AddParam(user_id);
			client.AddParam(mail);
			client.SetDataType("@user_id", SqlDbType.VarChar);
			client.SetDataType("@mail", SqlDbType.VarChar);
			var users_state_data = client.Select();

			if (users_state_data != null)
			{
				if (users_state_data["user_id"]?.ToString() == user_id)
				{
					return Results.BadRequest(new { message = "指定したユーザIDは既に使用されています。"});
				}
				if (users_state_data["mail"]?.ToString() == mail)
				{
					return Results.BadRequest(new { message = "既に登録済みのメールアドレスです。"});
				}
				return Results.BadRequest(new { message = "不明なエラー。"});
			}


			// 一定時間前に送信してたら
			client.Add("SELECT mail");
			client.Add("FROM pre_users");
			client.Add("WHERE mail = @mail");
			client.Add("	AND DATEADD(SECOND, -30, dbo.GET_TOKYO_DATETIME()) < updt;");
			client.AddParam(mail);
			client.SetDataType("@mail", SqlDbType.VarChar);
			if (client.Select() != null) return Results.BadRequest(new { message = "30秒以上間隔を開けてください。"});

			// トークンをセット
			string token = Guid.NewGuid().ToString("N");
			client.Add($"EXEC set_mail_token @mail = '{mail.Replace("'", "''")}', @token = '{token.Replace("'", "''")}', @user_id = '{user_id.Replace("'", "''")}', @user_name = N'{user_name.Replace("'", "''")}', @pw = '{Util.Hasher_sha256($"@{password}@")}', @comment = {(comment != null ? $"'{comment.Replace("'", "''")}'" : "null")}, @user_icon = {(user_icon != null ? $"'{user_icon.Replace("'", "''")}'" : "null")};"); // SQLインジェクション攻撃対策
			client.Execute();

			MailSetting mailSetting = new()
			{
				MailTo = mail,
				MailFrom = Env.SMTPSERVER_USER,
				Subject = "【simple-quiz】仮会員登録",
				Body = $"以下のリンクから会員登録を完成させてください。\r\nリンクの有効期限は10分です。\r\n\r\nhttps://{Env.DOMAIN}/register?token={token}",
			};
			if (MailClient.Send(mailSetting))
			{
				return Results.Ok(new {});
			}
			else
			{
				return Results.Problem($"メールの送信に失敗しました。");
			}
		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}


    /// <summary>
    /// 【完成】本登録処理を実行します。
    /// </summary>
	/// <remarks>
	/// Sample request:
	///
	/// 	POST /auth/signup
	/// 	{
	/// 		"token": "fba8c49f09f140d693ddf2a33491a82e",
	/// 		"user_id": "hogehoge",
	/// 		"password": "foofoo",
	/// 		"comment": "hogefoo"
	/// 	}
	///
	/// </remarks>
	/// <param name="signUpStruct"></param>
	/// <response code="200">正常に仮会員登録処理が完了しました。</response>
	/// <response code="400">不正なメールアドレスが指定されました。</response>
	/// <response code="500">会員登録処理中に例外が発生しました。</response>
	[Route("auth/signup")]
	[Produces("application/json")]
	[HttpPost]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static IResult SignUp(SignUpStruct signUpStruct, [FromHeader(Name = "Authorization")] string session_id)
	{
		string token = signUpStruct.token;
		try
		{

			DBClient client = new();

			// トークンのチェック

			client.Add("SELECT mail");
			client.Add("FROM pre_users");
			client.Add("WHERE token = @token");
			client.Add("	AND DATEADD(HOUR, -1, dbo.GET_TOKYO_DATETIME()) < updt");
			client.AddParam(token);
			client.SetDataType("@token", SqlDbType.VarChar);
			var result = client.Select();
			if (result == null) return Results.BadRequest(new { message = "指定したトークンは無効です。"});


			// 本登録処理
			client.Add("SELECT user_id, mail, user_name, pw, comment, user_icon");
			client.Add("FROM pre_users");
			client.Add("WHERE token = @token;");
			client.AddParam(token);
			client.SetDataType("token", SqlDbType.VarChar);
			var pre_users_data = client.Select();

			if (pre_users_data == null) return Results.BadRequest(new {message = "指定したトークンは無効です。"});

			var user_id = pre_users_data["user_id"]?.ToString();
			var mail = pre_users_data["mail"]?.ToString();
			var user_name = pre_users_data["user_name"]?.ToString();
			var password = pre_users_data["pw"]?.ToString();
			var comment = pre_users_data["comment"]?.ToString();
			var user_icon = pre_users_data["user_icon"]?.ToString();

			client.Add("INSERT INTO users(user_id, mail, user_name, pw, comment, user_icon)");
			client.Add("VALUES(@user_id, @mail, @user_name, @pw, @comment, @user_icon);");
			client.AddParam(user_id);
			client.AddParam(mail);
			client.AddParam(user_name);
			client.AddParam(password);
			client.AddParam(comment);
			client.AddParam(user_icon != null ? user_icon : DBNull.Value);
			client.SetDataType("@user_id", SqlDbType.VarChar);
			client.SetDataType("@mail", SqlDbType.VarChar);
			client.SetDataType("@user_name", SqlDbType.NVarChar);
			client.SetDataType("@pw", SqlDbType.VarChar);
			client.SetDataType("@comment", SqlDbType.NVarChar);
			client.SetDataType("@user_icon", SqlDbType.VarChar);
			client.Execute();

			client.Add("DELETE FROM pre_users");
			client.Add("WHERE token = @token;");
			client.AddParam(token);
			client.SetDataType("@token", SqlDbType.VarChar);
			client.Execute();

			// 現行のセッションと紐づけ
			client.Add("UPDATE sessions");
			client.Add("SET user_id = @user_id");
			client.Add("WHERE session_id = @session_id;");
			client.AddParam(user_id);
			client.AddParam(session_id);
			client.SetDataType("@user_id", SqlDbType.VarChar);
			client.SetDataType("@session_id", SqlDbType.VarChar);
			client.Execute();

			return Results.Ok(new {});
		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}




    /// <summary>
    /// 【完成】セッション管理用のトークンを無効化します。
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Delete /auth/signout
	/// 	
	/// </remarks>
    /// <returns>
	/// {}
    /// </returns>
	/// <response code="200">正常にトークンの無効化処理が実行されました。</response>
	/// <response code="500">トークン無効化処理中に例外が発生しました。</response>
    [HttpDelete]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static dynamic SignOut([FromHeader(Name = "Authorization")] string session_id)
	{
		try
		{
			DBClient client = new();
			client.Add("DELETE FROM sessions");
			client.Add("WHERE session_id = @session_id;");
			client.AddParam(session_id);
			client.SetDataType("@session_id", SqlDbType.VarChar);
			client.Execute();
			return Results.Ok(new {});
		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}


    /// <summary>
    /// 【完成】サインイン処理
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Post /auth/signin
	/// 	{
	/// 		"uid": "メールアドレス or ユーザID",
	/// 		"pw": "p@ssw0rd"
	/// 	}
	/// 	
	/// </remarks>
    /// <returns>
	/// {}
    /// </returns>
	/// <response code="200">正常にサインイン処理が実行されました。</response>
	/// <response code="400">指定したパラメタに不備があります。</response>
	/// <response code="401">認証に失敗しました。</response>
	/// <response code="500">予期せぬ例外が発生しました。</response>
    [HttpPost]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static dynamic SignIn(SignInStruct signInStruct, [FromHeader(Name = "Authorization")] string session_id)
	{
		try
		{
			string uid = signInStruct.uid;
			string password = signInStruct.password;


			// メールアドレスのチェック
			if (!Regex.IsMatch(uid, @"^[a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$") && (uid.Length < 3 || 16 < uid.Length))
			{
				return Results.BadRequest(new { message = "メールアドレス、またはユーザIDの形式が不正です。"});
			}

			if (!Regex.IsMatch(password, @"^[a-zA-Z0-9-/:-@\[-\`\{-\~]+$"))
			{
				return Results.BadRequest(new { message = "パスワードは空白類似文字を除いた半角英数字のみで構成してください。"});
			}
			if (password.Length < 8 || 32 < password.Length)
			{
				return Results.BadRequest(new { message = "パスワードは8文字以上、32文字以内で入力してください。"});
			}

			// 認証チェック
			var hashed_password = Util.Hasher_sha256($"@{password}@");
			if (hashed_password == null) return Results.Problem();

			DBClient client = new();
			client.Add("SELECT user_id");
			client.Add("FROM users");
			client.Add("WHERE (user_id = @uid OR mail = @uid) AND pw = @pw;");
			client.AddParam(uid);
			client.AddParam(hashed_password);
			client.SetDataType("@uid", SqlDbType.VarChar);
			client.SetDataType("@pw", SqlDbType.VarChar);

			var user_id = client.Select()?["user_id"]?.ToString();

			if (user_id == null)
			{
				return Results.Unauthorized();
			}

			client.Add("UPDATE sessions");
			client.Add("SET");
			client.Add("	user_id = @user_id");
			client.Add("WHERE session_id = @session_id;");
			client.AddParam(user_id);
			client.AddParam(session_id);
			client.SetDataType("@user_id", SqlDbType.VarChar);
			client.SetDataType("@session_id", SqlDbType.VarChar);
			client.Execute();

			return Results.Ok(new {});
		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}



    /// <summary>
    /// 【完成】ユーザID使用判定
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Get /auth/caniuse/hogehoge
	/// 	
	/// </remarks>
    /// <returns>
	/// {
	/// 	"caniuse": true
	/// }
    /// </returns>
	/// <response code="200">正常に結果が取得できました。</response>
	/// <response code="400">指定したパラメタが不正です。</response>
	/// <response code="500">予期せぬ例外が発生しました。</response>
    [HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static dynamic CanIUse(string user_id)
	{
		try
		{
			if (!Regex.IsMatch(user_id, @"^[a-zA-Z0-9_\-]+$"))
			{
				return Results.BadRequest(new {message = "ユーザ名は半角英数字と「_(アンダースコア)」「-(ハイフン)」のみで構成して下さい。"});
			}
			
			if (user_id.Length < 3 || 16 < user_id.Length)
			{
				return Results.BadRequest(new {message = "ユーザIDは3文字以上、16文字以内で指定してください。"});
			}
			
			DBClient client = new();

			client.Add("SELECT user_id");
			client.Add("FROM users");
			client.Add("WHERE user_id = @user_id");
			client.Add("UNION");
			client.Add("SELECT user_id");
			client.Add("FROM pre_users");
			client.Add("WHERE user_id = @user_id;");
			client.AddParam(user_id);
			client.SetDataType("@user_id", SqlDbType.VarChar);

			bool usable = client.Select() == null;

			return Results.Ok(new {caniuse = usable});

		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}



	
    /// <summary>
    /// トークンからメールアドレスを逆引き
    /// </summary>
	/// <remarks>
	/// Sample request:
	/// 	
	/// 	Get /auth/mail/82efba8c49f09f140d693ddf2a33491a
	/// 	
	/// </remarks>
    /// <returns>
	/// {
	/// 	"mail": "osawakoki@example.com"
	/// }
    /// </returns>
	/// <response code="200">正常に結果が取得できました。</response>
	/// <response code="400">指定したパラメタが不正です。</response>
	/// <response code="500">予期せぬ例外が発生しました。</response>
    [HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	internal static dynamic LookUpMail(string token)
	{
		try
		{
			DBClient client = new();

			client.Add("SELECT mail");
			client.Add("FROM pre_users");
			client.Add("WHERE token = @token;");
			client.AddParam(token);
			client.SetDataType("@token", SqlDbType.VarChar);

			var result = client.Select();

			if (result == null) return Results.BadRequest(new {message = "無効なトークンです。"});

			return Results.Ok(new {mail = result["mail"]?.ToString()});

		}
		catch (Exception ex)
		{
			return Results.Problem($"{ex}");
		}
	}



}

