using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicCore
{
    /// <summary>
    /// A Storable record with a unique identifier
    /// </summary>
    public interface IRecord
    {
        /// <summary>
        /// The record's unique identifier
        /// </summary>
        public string Id { get; set; }
    }
}
