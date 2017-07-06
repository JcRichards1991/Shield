﻿namespace Our.Shield.Core.UI
{
    /// <summary>
    /// Container class for categories of constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Constants for the application
        /// </summary>
        public static class App
        {
            /// <summary>
            /// Custom section Name
            /// </summary>
            public const string Name = nameof(Shield);

            /// <summary>
            /// Custom section Alias
            /// </summary>
            public const string Alias = "shield";

            /// <summary>
            /// Custom section Icon
            /// </summary>
            public const string Icon = "icon-shield";
        }

        /// <summary>
        /// Constants for the tree controller
        /// </summary>
        public static class Tree
        {
            /// <summary>
            /// Tree Alias
            /// </summary>
            public const string Alias = "environments";

            /// <summary>
            /// Tree Title
            /// </summary>
            public const string Title = App.Name;

            /// <summary>
            /// The Environment Node Root Id
            /// </summary>
            public const int EnvironmentsRootId = 0;
        }
    }
}