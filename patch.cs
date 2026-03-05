            DatabasesView.Filter = (obj) =>
            {
                if (string.IsNullOrEmpty(searchText)) return true;
                if (obj is string str)
                {
                    return str.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return false;
            };
            DatabasesView.Refresh();
        }

        private class DatabaseSortComparer : System.Collections.IComparer
        {
            private readonly string _searchText;
