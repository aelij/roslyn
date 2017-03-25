﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.CodeAnalysis.BuildTasks {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class ErrorString {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal ErrorString() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.CodeAnalysis.BuildTasks.ErrorString", typeof(ErrorString).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3883: Unexpected exception: .
        /// </summary>
        internal static string Compiler_UnexpectedException {
            get {
                return ResourceManager.GetString("Compiler_UnexpectedException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to check the content hash of the destination ref assembly &apos;{0}&apos;. It will be overwritten..
        /// </summary>
        internal static string CopyRefAssembly_BadDestination1 {
            get {
                return ResourceManager.GetString("CopyRefAssembly_BadDestination1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to check the content hash of the source ref assembly &apos;{0}&apos;: {1}
        ///{2}.
        /// </summary>
        internal static string CopyRefAssembly_BadSource3 {
            get {
                return ResourceManager.GetString("CopyRefAssembly_BadSource3", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reference assembly &quot;{0}&quot; already has latest information. Leaving it untouched..
        /// </summary>
        internal static string CopyRefAssembly_SkippingCopy1 {
            get {
                return ResourceManager.GetString("CopyRefAssembly_SkippingCopy1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3053: The assembly alias &quot;{1}&quot; on reference &quot;{0}&quot; contains illegal characters..
        /// </summary>
        internal static string Csc_AssemblyAliasContainsIllegalCharacters {
            get {
                return ResourceManager.GetString("Csc_AssemblyAliasContainsIllegalCharacters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3051: The parameter to the compiler is invalid.  {0}.
        /// </summary>
        internal static string Csc_InvalidParameter {
            get {
                return ResourceManager.GetString("Csc_InvalidParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3052: The parameter to the compiler is invalid, &apos;{0}{1}&apos; will be ignored..
        /// </summary>
        internal static string Csc_InvalidParameterWarning {
            get {
                return ResourceManager.GetString("Csc_InvalidParameterWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The string &quot;{0}&quot; cannot be converted to a boolean (true/false) value..
        /// </summary>
        internal static string General_CannotConvertStringToBool {
            get {
                return ResourceManager.GetString("General_CannotConvertStringToBool", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3081: A problem occurred while trying to set the &quot;{0}&quot; parameter for the IDE&apos;s in-process compiler. {1}.
        /// </summary>
        internal static string General_CouldNotSetHostObjectParameter {
            get {
                return ResourceManager.GetString("General_CouldNotSetHostObjectParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3105: The item &quot;{0}&quot; was specified more than once in the &quot;{1}&quot; parameter.  Duplicate items are not supported by the &quot;{1}&quot; parameter..
        /// </summary>
        internal static string General_DuplicateItemsNotSupported {
            get {
                return ResourceManager.GetString("General_DuplicateItemsNotSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3083: The item &quot;{0}&quot; was specified more than once in the &quot;{1}&quot; parameter and both items had the same value &quot;{2}&quot; for the &quot;{3}&quot; metadata.  Duplicate items are not supported by the &quot;{1}&quot; parameter unless they have different values for the &quot;{3}&quot; metadata..
        /// </summary>
        internal static string General_DuplicateItemsNotSupportedWithMetadata {
            get {
                return ResourceManager.GetString("General_DuplicateItemsNotSupportedWithMetadata", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Expected file &quot;{0}&quot; does not exist..
        /// </summary>
        internal static string General_ExpectedFileMissing {
            get {
                return ResourceManager.GetString("General_ExpectedFileMissing", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3087: An incompatible host object was passed into the &quot;{0}&quot; task.  The host object for this task must implement the &quot;{1}&quot; interface..
        /// </summary>
        internal static string General_IncorrectHostObject {
            get {
                return ResourceManager.GetString("General_IncorrectHostObject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Item &quot;{0}&quot; has attribute &quot;{1}&quot; with value &quot;{2}&quot; that could not be converted to &quot;{3}&quot;..
        /// </summary>
        internal static string General_InvalidAttributeMetadata {
            get {
                return ResourceManager.GetString("General_InvalidAttributeMetadata", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The IDE&apos;s in-process compiler does not support the specified values for the &quot;{0}&quot; parameter.  Therefore, this task will fallback to using the command-line compiler..
        /// </summary>
        internal static string General_ParameterUnsupportedOnHostCompiler {
            get {
                return ResourceManager.GetString("General_ParameterUnsupportedOnHostCompiler", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3104: The referenced assembly &quot;{0}&quot; was not found. If this assembly is produced by another one of your projects, please make sure to build that project before building this one..
        /// </summary>
        internal static string General_ReferenceDoesNotExist {
            get {
                return ResourceManager.GetString("General_ReferenceDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3082: Task failed because &quot;{0}&quot; was not found..
        /// </summary>
        internal static string General_ToolFileNotFound {
            get {
                return ResourceManager.GetString("General_ToolFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Shared compilation failed; falling back to tool: {0}.
        /// </summary>
        internal static string SharedCompilationFallback {
            get {
                return ResourceManager.GetString("SharedCompilationFallback", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using shared compilation with compiler from directory: {0}.
        /// </summary>
        internal static string UsingSharedCompilation {
            get {
                return ResourceManager.GetString("UsingSharedCompilation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3401: &quot;{1}&quot; is an invalid value for the &quot;{0}&quot; parameter.  The valid values are: {2}.
        /// </summary>
        internal static string Vbc_EnumParameterHasInvalidValue {
            get {
                return ResourceManager.GetString("Vbc_EnumParameterHasInvalidValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &quot;{1}&quot; is an invalid value for the &quot;{0}&quot; parameter..
        /// </summary>
        internal static string Vbc_ParameterHasInvalidValue {
            get {
                return ResourceManager.GetString("Vbc_ParameterHasInvalidValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSB3402: There was an error creating the pdb file &quot;{0}&quot;. {1}.
        /// </summary>
        internal static string Vbc_RenamePDB {
            get {
                return ResourceManager.GetString("Vbc_RenamePDB", resourceCulture);
            }
        }
    }
}
