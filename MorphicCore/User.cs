using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicCore
{
    public class User
    {

        public User()
        {
            this.identifier = Guid.NewGuid().ToString();
        }

        public User(string identifier)
        {
            this.identifier = identifier;
        }

        public string identifier { get; set; }
    }
}
