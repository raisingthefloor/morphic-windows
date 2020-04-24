// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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

using System;
using MorphicCore;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace MorphicSettings
{
    public abstract class SettingsHandler
    {

        public abstract bool Apply(object? value);

        private static readonly Dictionary<Preferences.Key, Type> handlerTypesByKey = new Dictionary<Preferences.Key, Type>();

        public static void Register(Type type, Preferences.Key key)
        {
            handlerTypesByKey[key] = type;
        }

        public static SettingsHandler? Handler(IServiceProvider provider, Preferences.Key key)
        {
            if (handlerTypesByKey.TryGetValue(key, out var type))
            {
                var instance = provider.GetService(type);
                if (instance is SettingsHandler handler)
                {
                    return handler;
                }
            }
            return null;
        }
    }

    public class SettingsHandlerBuilder
    {
        public SettingsHandlerBuilder(IServiceCollection services)
        {
            this.services = services;
        }

        IServiceCollection services;

        public void AddHandler(Type type, Preferences.Key key)
        {
            services.AddTransient(type);
            SettingsHandler.Register(type, key);
        }
    }
}
