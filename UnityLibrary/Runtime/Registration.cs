using System.Collections.Generic;

namespace Intercom
{
    public class Registration
    {
        private string email = "";
        private string userId = "";
        private Dictionary<string, object> attributes;

        public static Registration Create()
        {
            return new Registration();
        }

        public Registration WithEmail(string email)
        {
            if (!string.IsNullOrEmpty(email))
            {
                this.email = email;
            }
            return this;
        }

        public Registration WithUserId(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                this.userId = userId;
            }
            return this;
        }

        public Registration WithUserAttributes(Dictionary<string, object> attributes)
        {
            if (attributes != null && attributes.Count > 0)
            {
                this.attributes = new Dictionary<string, object>(attributes);
            }
            return this;
        }

        public string GetEmail() => email;
        public string GetUserId() => userId;
        public Dictionary<string, object> GetAttributes() => attributes;
    }
}
