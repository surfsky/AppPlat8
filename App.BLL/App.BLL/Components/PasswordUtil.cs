using System;
using System.Security.Cryptography;

namespace App.Components
{
	/// <summary>
	/// 密码工具类，用于密码的加密、解密等操作。
	/// </summary>
	public class PasswordUtil
	{
        private const int saltLength = 4;
        public PasswordUtil() { }


        /// <summary>比较密码是否匹配</summary>
        /// <param name="dbPassword">数据库中的密码（加密后的）</param>
        /// <param name="userPassword">用户输入的密码</param>
        /// <returns>如果密码匹配则返回true，否则返回false</returns>
		public static bool ComparePasswords(string dbPassword,string userPassword)
		{
			byte[] dbPwd = Convert.FromBase64String(dbPassword);
			byte[] hashedPwd = HashString(userPassword);
			if(dbPwd.Length ==0 || hashedPwd.Length ==0 || dbPwd.Length !=hashedPwd.Length + saltLength)
				return false;

			byte[] saltValue = new byte[saltLength];
			int saltOffset = hashedPwd.Length;
			for (int i = 0; i < saltLength; i++)
				saltValue[i] = dbPwd[saltOffset + i];
			byte[] saltedPassword = CreateSaltedPassword(saltValue, hashedPwd);
		
			// compare the values
			return CompareByteArray(dbPwd, saltedPassword);
		}

        /// <summary>创建数据库密码（加密后的密码）</summary>
		public static string CreateDbPassword(string userPassword)
		{
			byte[] unsaltedPassword = HashString(userPassword);

			//Create a salt value
			byte[] saltValue = new byte[saltLength];
			var rng = RandomNumberGenerator.Create(); // new RNGCryptoServiceProvider();
			rng.GetBytes(saltValue);
			byte[] saltedPassword = CreateSaltedPassword(saltValue, unsaltedPassword);
			return Convert.ToBase64String(saltedPassword);
		}

		//----------------------------------------------------
		// 密码哈希计算
		//----------------------------------------------------
		/// <summary>对字符串进行哈希计算</summary>
		private static byte[] HashString(string str)
		{
			byte[] pwd = System.Text.Encoding.UTF8.GetBytes(str);

			SHA1 sha1 = SHA1.Create();
			byte[] saltedPassword = sha1.ComputeHash(pwd);
			return saltedPassword;
		}
		private static bool CompareByteArray(byte[] array1, byte[] array2)
		{
			if (array1.Length != array2.Length)
				return false;
			for (int i = 0; i < array1.Length; i++)
			{
				if (array1[i] != array2[i])
					return false;
			}
			return true;
		}
		// create a salted password given the salt value
		private static byte[] CreateSaltedPassword(byte[] saltValue, byte[] unsaltedPassword)
		{
			// add the salt to the hash
			byte[] rawSalted  = new byte[unsaltedPassword.Length + saltValue.Length]; 
			unsaltedPassword.CopyTo(rawSalted,0);
			saltValue.CopyTo(rawSalted,unsaltedPassword.Length);
			
			//Create the salted hash			
			SHA1 sha1 = SHA1.Create();
			byte[] saltedPassword = sha1.ComputeHash(rawSalted);

			// add the salt value to the salted hash
			byte[] dbPassword  = new byte[saltedPassword.Length + saltValue.Length];
			saltedPassword.CopyTo(dbPassword,0);
			saltValue.CopyTo(dbPassword,saltedPassword.Length);

			return dbPassword;
		}

	}
}
