using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ExhaleCreativity
{
    internal static class Helpers
    {
        internal static readonly TextInfo EnglishTextInfo = new CultureInfo("en-US", useUserOverride: false).TextInfo;

        /// <summary>
        /// For stripe customers, return the name as First L.
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        internal static string FormatDefaultName(string fullName)
        {

            var split = fullName.Split(' ');
            if (split.Length == 1)
                return EnglishTextInfo.ToTitleCase(fullName.Trim());

            var firstBit = string.Empty;
            var lastBit = string.Empty;
            var i = 0;

            foreach (var section in split)
            {
                if (!string.IsNullOrWhiteSpace(section))
                {
                    i++;
                    if (i == 1)
                        firstBit = section.Trim().ToLower(); // for proper title case
                    else
                        lastBit = section.Trim().ToLower(); // for proper title case
                }
                if (i == 2)
                    break;
            }

            // from here assume first position is first name;
            // take first letter of second string for what we consider last initial
            return $"{EnglishTextInfo.ToTitleCase(firstBit)} {lastBit.ToUpper().Substring(0, 1)}";
        }

        /// <summary>
        /// Hashes an email with MD5.  Suitable for use with Gravatar profileimage urls
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        internal static string GetUniqueMemberId(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return null;
            }

            // Create a new instance of the MD5CryptoServiceProvider object.
            using var md5Hasher = MD5.Create();
            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(email.Trim()));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new();

            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();  // Return the hexadecimal string.
        }
    }

}