using System;
using System.Collections.Generic;
using System.Linq;

namespace SMS_Search.Services
{
    /// <summary>
    /// Bidirectional mapper between LOC SMS Dictionary (DCT) names in Fields.csv
    /// and SQL Server table names.
    /// </summary>
    public static class DictionaryTableMapper
    {
        private static readonly Dictionary<string, string> SqlToDct = new(StringComparer.OrdinalIgnoreCase)
        {
            // Item tables
            ["ALT_TAB"] = "ALT",
            ["ALT_DEL"] = "ALT",
            ["CLS_TAB"] = "CLSITM",
            ["CLS_DEL"] = "CLSITM",
            ["CLT_ITM_TAB"] = "CLTITM",
            ["CLT_ITM_DEL"] = "CLTITM",
            ["COST_TAB"] = "COST",
            ["COST_DEL"] = "COST",
            ["DSD_TAB"] = "DSD",
            ["DSD_DEL"] = "DSD",
            ["ECL_TAB"] = "ECL",
            ["ECL_DEL"] = "ECL",
            ["FIL_TAB"] = "FIL",
            ["ITD_TAB"] = "ITD",
            ["ITD_DEL"] = "ITD",
            ["ITZ_TAB"] = "ITZ",
            ["ITZ_DEL"] = "ITZ",
            ["KIT_TAB"] = "KIT",
            ["KIT_DEL"] = "KIT",
            ["LOC_TAB"] = "LOC",
            ["LOC_DEL"] = "LOC",
            ["OBJ_TAB"] = "OBJ",
            ["OBJ_DEL"] = "OBJ",
            ["PRICE_TAB"] = "PRICE",
            ["PRICE_DEL"] = "PRICE",
            ["POS_TAB"] = "POS",
            ["POS_DEL"] = "POS",
            ["SCL_TAB"] = "SCAL_ITM",
            ["SCL_DEL"] = "SCAL_ITM",
            ["SHF_TAB"] = "SHF",
            ["MOD_TAB"] = "MOD",
            ["MOD_ITM_TAB"] = "MODITM",

            // Auxiliary tables
            ["BTL_TAB"] = "BTL",
            ["CAT_TAB"] = "CAT",
            ["CLF_SDP_TAB"] = "CLFSDP",
            ["CLS_AUX"] = "CLSAUX",
            ["CPN_TAB"] = "CPN",
            ["DELV_TAB"] = "DELV",
            ["DEPT_TAB"] = "DEPT",
            ["FAM_TAB"] = "FAM",
            ["LIKE_TAB"] = "LIK",
            ["MIX_TAB"] = "MIX",
            ["MIX_DEL"] = "MIX",
            ["PUB_TAB"] = "PUB",
            ["REASON_TAB"] = "REASON",
            ["RES_TAB"] = "RES",
            ["ROUTE_TAB"] = "ROUTE",
            ["RPC_TAB"] = "RPC",
            ["SCL_NUT_TAB"] = "SCAL_NUT",
            ["SCL_NUT_DEL"] = "SCAL_NUT",
            ["SCL_TXT_TAB"] = "SCAL_TXT",
            ["SCL_TXT_DEL"] = "SCAL_TXT",
            ["SCL_STO_TAB"] = "SCAL_STO",
            ["SDP_TAB"] = "SDP",
            ["TAR_TAB"] = "TAR",
            ["VENDOR_TAB"] = "VENDOR",
            ["VENDOR_DEL"] = "VENDOR",

            // System tables
            ["CLK_TAB"] = "CLK",
            ["CLK_DEL"] = "CLK",
            ["CFG_TAB"] = "CFG",
            ["DRIL_FILE"] = "DRIL_FILE",
            ["DRIL_PAGE"] = "DRIL_PAGE",
            ["FCT_TAB"] = "FCT",
            ["FCZ_TAB"] = "FCZ",
            ["LNK_TAB"] = "LNK",
            ["LVL_TAB"] = "LVL",
            ["PERIOD_TAB"] = "PERIOD",
            ["PROFILE_TAB"] = "PROFILE",
            ["RUN_TAB"] = "RUN",
            ["STD_TAB"] = "STD",
            ["STO_TAB"] = "STO",
            ["TER_TAB"] = "TER",
            ["TLZ_TAB"] = "TLZ",
            ["TZS_TAB"] = "TZS",

            // Customer tables
            ["CLF_TAB"] = "CLF",
            ["CLG_TAB"] = "CLG",
            ["CLL_TAB"] = "CLL",
            ["CLL_DEL"] = "CLL",
            ["CLR_TAB"] = "CLR",
            ["CLT_TAB"] = "CLT",
            ["CLT_DEL"] = "CLT",

            // Batch tables
            ["ALLOW_BAT"] = "ALLOW",
            ["OALLOW_BAT"] = "ALLOW",
            ["FBBACK_BAT"] = "BBACK",
            ["OBBACK_BAT"] = "BBACK",
            ["FCSTBRK_BAT"] = "CSTBRK",
            ["OCSTBRK_BAT"] = "CSTBRK",
            ["FDISC_BAT"] = "DISC",
            ["ODISC_BAT"] = "DISC",
            ["FCOST_BAT"] = "FCOST",
            ["OCOST_BAT"] = "FCOST",
            ["FECL_BAT"] = "FECL",
            ["OECL_BAT"] = "FECL",
            ["FOBJ_BAT"] = "FOBJ",
            ["OOBJ_BAT"] = "FOBJ",
            ["FPOS_BAT"] = "FPOS",
            ["OPOS_BAT"] = "FPOS",
            ["FPRICE_BAT"] = "FPRICE",
            ["OPRICE_BAT"] = "FPRICE",
            ["FSPRICE_BAT"] = "FSPRICE",
            ["OSPRICE_BAT"] = "FSPRICE",
            ["FTPRICE_BAT"] = "FTPRICE",
            ["OTPRICE_BAT"] = "FTPRICE",
            ["FGCOST_BAT"] = "GCOST",
            ["OGCOST_BAT"] = "GCOST",
            ["FGPRICE_BAT"] = "GPRICE",
            ["OGPRICE_BAT"] = "GPRICE",
            ["HEADER_BAT"] = "HEADER",
            ["HEADER_DCT"] = "HEADER",
            ["HEADER_DEL"] = "HEADER",
            ["HEADER_EXE"] = "HEADER",
            ["HEADER_INS"] = "HEADER",
            ["HEADER_OLD"] = "HEADER",
            ["HEADER_SAV"] = "HEADER",
            ["FINSTORE_BAT"] = "INSTORE",
            ["OINSTORE_BAT"] = "INSTORE",
            ["REBATE_BAT"] = "REBATE",
            ["OREBATE_BAT"] = "REBATE",
            ["FREDEEM_BAT"] = "REDEEM",
            ["OREDEEM_BAT"] = "REDEEM",

            // Transaction details tables
            ["TRS_CLK"] = "TRSCLK",
            ["TRS_CLT"] = "TRSCLT",
            ["TRS_DPT"] = "TRSDPT",
            ["TRS_FIN"] = "TRSFIN",
            ["TRS_ITM"] = "TRSITM",
            ["TRS_LOG"] = "TRSLOG",
            ["TRS_SUB"] = "TRSSUB",
            ["TRS_VND"] = "TRSVND",

            // Report tables
            ["RPT_CLK_D"] = "RPTCLK",
            ["RPT_CLK_F"] = "RPTCLK",
            ["RPT_CLK_M"] = "RPTCLK",
            ["RPT_CLK_P"] = "RPTCLK",
            ["RPT_CLK_W"] = "RPTCLK",
            ["RPT_CLK_Y"] = "RPTCLK",
            ["RPT_CLK_N"] = "RPTCLKN",
            ["RPT_CLT_D"] = "RPTCLT",
            ["RPT_CLT_F"] = "RPTCLT",
            ["RPT_CLT_M"] = "RPTCLT",
            ["RPT_CLT_P"] = "RPTCLT",
            ["RPT_CLT_W"] = "RPTCLT",
            ["RPT_CLT_Y"] = "RPTCLT",
            ["RPT_CLT_N"] = "RPTCLTN",
            ["RPT_CLT_ITM_D"] = "RPTCLTITM",
            ["RPT_CLT_ITM_N"] = "RPTCLTITMN",
            ["RPT_DPT"] = "RPTDPT",
            ["RPT_FIN"] = "RPTFIN",
            ["RPT_HOU"] = "RPTHOU",
            ["RPT_ITM_D"] = "RPTITM",
            ["RPT_ITM_F"] = "RPTITM",
            ["RPT_ITM_M"] = "RPTITM",
            ["RPT_ITM_P"] = "RPTITM",
            ["RPT_ITM_W"] = "RPTITM",
            ["RPT_ITM_Y"] = "RPTITM",
            ["RPT_ITM_N"] = "RPTITMN",
            ["RPT_SUB"] = "RPTSUB",
            ["RPT_VND"] = "RPTVND",
            ["WATCH_TAB"] = "WATCH",

            // Receiving, Sale & Inventory transaction tables (+ _SAV)
            ["REC_BAT"] = "HDRREC",
            ["REC_HDR"] = "HDRREC",
            ["REC_HDR_SAV"] = "HDRREC",
            ["REC_REG"] = "REGREC",
            ["REC_REG_SAV"] = "REGREC",
            ["REC_TTL"] = "TTL",
            ["REC_TTL_SAV"] = "TTL",
            ["SAL_BAT"] = "HDRSAL",
            ["SAL_DET"] = "DETSAL",
            ["SAL_HDR"] = "HDRSAL",
            ["SAL_HDR_SAV"] = "HDRSAL",
            ["SAL_REG"] = "REGSAL",
            ["SAL_REG_SAV"] = "REGSAL",
            ["SAL_TTL"] = "TTL",
            ["SAL_TTL_SAV"] = "TTL",
            ["INV_BAT"] = "HDRINV",
            ["INV_HDR"] = "HDRINV",
            ["INV_HDR_SAV"] = "HDRINV",
            ["INV_REG"] = "REGINV",
            ["INV_REG_SAV"] = "REGINV",
            ["INV_TTL"] = "TTL",
            ["INV_TTL_SAV"] = "TTL",

            // Internal tables
            ["ACC_TAB"] = "ACC",
            ["BIO_TAB"] = "BIO",
            ["BIO_DEL"] = "BIO",
            ["DATA_REG"] = "DATA_REG",
            ["DSS_TAB"] = "DSS",
            ["EXC_TAB"] = "EXC",
            ["HOOK_TAB"] = "HOOK",
            ["LABEL_TAB"] = "LABEL",
            ["PAY_TAB"] = "PAY",
            ["RB_FIELDS"] = "RBF",
            ["RB_JOINS"] = "RBJ",
            ["RB_TABLES"] = "RBT",
            ["RENT_TAB"] = "RENT",
            ["SYS_TAB"] = "SYS",
            ["TASK_TAB"] = "TASK",
            ["UNT_TAB"] = "UNT"
        };

        private static readonly Dictionary<string, string> DctToPrimarySqlTable;

        static DictionaryTableMapper()
        {
            DctToPrimarySqlTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in SqlToDct)
            {
                if (!DctToPrimarySqlTable.ContainsKey(kvp.Value))
                {
                    DctToPrimarySqlTable[kvp.Value] = kvp.Key;
                }
            }
        }

        /// <summary>
        /// Resolves the Dictionary (DCT) name for a SQL Server table name.
        /// Falls back to stripping common suffixes (_TAB, _DEL, _BAT, _SAV) if not in the explicit map.
        /// </summary>
        public static string GetDctForSqlTable(string sqlTableName)
        {
            if (string.IsNullOrWhiteSpace(sqlTableName))
                return string.Empty;

            string clean = sqlTableName.Trim();
            if (SqlToDct.TryGetValue(clean, out string? dct))
                return dct;

            // Strip common suffixes as heuristic for custom or newer tables
            string[] suffixes = { "_TAB", "_DEL", "_BAT", "_SAV" };
            foreach (var suffix in suffixes)
            {
                if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && clean.Length > suffix.Length)
                {
                    return clean.Substring(0, clean.Length - suffix.Length);
                }
            }

            return clean;
        }

        /// <summary>
        /// Resolves the primary SQL Server table name for a Dictionary (DCT) column name.
        /// </summary>
        public static string GetPrimarySqlTableForDct(string dctName)
        {
            if (string.IsNullOrWhiteSpace(dctName))
                return string.Empty;

            string clean = dctName.Trim();
            if (DctToPrimarySqlTable.TryGetValue(clean, out string? sqlTable))
                return sqlTable;

            // Heuristic for custom tables defined in Fields2.csv (e.g. BUYERS -> BUYERS_TAB)
            return clean.EndsWith("_TAB", StringComparison.OrdinalIgnoreCase)
                ? clean
                : $"{clean}_TAB";
        }

        /// <summary>
        /// Checks whether a SQL table name or DCT name matches a target DCT column name.
        /// </summary>
        public static bool MatchesTableFilter(string dctName, string filterPattern, Func<string, string, bool> wildcardMatcher)
        {
            if (wildcardMatcher(dctName, filterPattern))
                return true;

            string primarySql = GetPrimarySqlTableForDct(dctName);
            if (wildcardMatcher(primarySql, filterPattern))
                return true;

            foreach (var kvp in SqlToDct.Where(x => string.Equals(x.Value, dctName, StringComparison.OrdinalIgnoreCase)))
            {
                if (wildcardMatcher(kvp.Key, filterPattern))
                    return true;
            }

            return false;
        }
    }
}

