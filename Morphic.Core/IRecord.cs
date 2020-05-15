using System;
using System.Collections.Generic;
using System.Text;

namespace Morphic.Core
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
