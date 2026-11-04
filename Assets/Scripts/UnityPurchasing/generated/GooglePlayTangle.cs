// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("ELT6slQEwaz368PMFmE5mwmiZgNTERJAzhUlz+SmxTgfwdoCFCZlDjgcuTtIMraPTS15XzcDEs7eAVzBFnCiaGz6iXsBhkeVafN2+Si0xznJnjmaLPRXW11ao25i9nAxcxqAotG9rNqvqIpB3QMVIXKbwEmq1wxKcK4I9S8XkKD0IEjb6xw7pPhTCKA9ExehH7JJb08UoIO/OfrDstL87Q7t3hLj5DpkJptFXwr1/JY+vLHhSRYJupytY027PUgrFFeQzuCf4b0bDjfzQ3U7gjrvU1p7S3+1LM8qUbnT17SHCMiaqVaakdT83AgdT6rBizm6mYu2vbKRPfM9TLa6urq+u7g5urS7izm6sbk5urq7d55FTsuebhp+uF+W+YV6vrm4uru6");
        private static int[] order = new int[] { 12,13,5,9,11,8,9,9,8,10,10,11,12,13,14 };
        private static int key = 187;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
