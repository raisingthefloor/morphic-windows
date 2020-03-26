using System;

namespace MorphicCore
{
    public class User
    {
        public string Id { get; set; } = "";
        public string? PreferencesId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
