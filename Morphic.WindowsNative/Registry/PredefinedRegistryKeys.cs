// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.WindowsNative;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class Registry
{
    private static Lazy<RegistryKey> _classesRootRegistryKey = new(() => new RegistryKey(PInvoke.AdvApi32.HKEY_CLASSES_ROOT));
    public static RegistryKey ClassesRoot
    {
        get
        {
            return _classesRootRegistryKey.Value;
        }
    }

    private static Lazy<RegistryKey> _currentUserRegistryKey = new(() => new RegistryKey(PInvoke.AdvApi32.HKEY_CURRENT_USER));
    public static RegistryKey CurrentUser
    {
        get
        {
            return _currentUserRegistryKey.Value;
        }
    }

    private static Lazy<RegistryKey> _localMachineRegistryKey = new(() => new RegistryKey(PInvoke.AdvApi32.HKEY_LOCAL_MACHINE));
    public static RegistryKey LocalMachine
    {
        get
        {
            return _localMachineRegistryKey.Value;
        }
    }

    private static Lazy<RegistryKey> _usersRegistryKey = new(() => new RegistryKey(PInvoke.AdvApi32.HKEY_USERS));
    public static RegistryKey Users
    {
        get
        {
            return _usersRegistryKey.Value;
        }
    }

    private static Lazy<RegistryKey> _currentConfigRegistryKey = new(() => new RegistryKey(PInvoke.AdvApi32.HKEY_CURRENT_CONFIG));
    public static RegistryKey CurrentConfig
    {
        get
        {
            return _currentConfigRegistryKey.Value;
        }
    }
}
