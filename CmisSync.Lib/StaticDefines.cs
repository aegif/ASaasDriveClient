//   CmisSync, a collaboration and sharing tool. Based on:
//      SparkleShare, a collaboration and sharing tool.
//      Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Reflection;
using System.Runtime.InteropServices;

// Name of the CmisSync library.
[assembly:AssemblyTitle ("CmisSync.Lib")]

// Version of the CmisSync library. It is used as the CmisSync version.
[assembly:AssemblyVersion ("1.0.13")]

// Copyright.
[assembly:AssemblyCopyright ("Copyright (c) 2010 Hylke Bons, Aegif and others")]

// Trademark.
[assembly:AssemblyTrademark ("CmisSync is a trademark of CmisSync Ltd.")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

[assembly: CLSCompliant(true)]

namespace CmisSync.Lib {

    /// <summary>
    /// CmisSync Constants.
    /// </summary>
    public class Defines {
        public const string INSTALL_DIR = "/usr/local/share/CmisSync";
    }
}
