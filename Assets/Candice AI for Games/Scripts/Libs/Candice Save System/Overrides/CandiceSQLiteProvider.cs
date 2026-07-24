#if CANDICE_LEGACY_MONO_SQLITE
using Mono.Data.Sqlite;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CandiceAIforGames.Data
{
#if CANDICE_LEGACY_MONO_SQLITE
    public class CandiceSQLiteProvider : CandiceProviderBase
    {
        private string query = "";
        private Dictionary<object, object> queryParameters = null;
        private string conStr = "";
        //private int OBJECT_TYPE = ObjectTypes.OBJECT_NONE;

        public CandiceSQLiteProvider(string conStr)
        {
            this.conStr = conStr;
        }
        #region CRUD METHODS
        public override int Delete(string serialNr = "")
        {
            //
            //Name            : int Delete(string serialNr)
            //Purpose         : Try to delete a row from the datastore
            //Re-use          : none
            //Input Parameter : string serialNr
            //                   - the serialNr of the object to delete in the  datastore
            //Output Type     : - int
            //                 0 : object found and deleted successfully
            //                -1 : object not deleted because the record was not found
            //
            int rc = 0;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;
            string deleteQuery = "";
            try
            {
                if (!query.Equals(""))
                {

                    sqlCon = new SqliteConnection(conStr);
                    sqlCon.Open();
                    //deleteQuery = string.Format("DELETE FROM Objects WHERE [serialNr] = '{0}'", serialNr);
                    deleteQuery = query;
                    sqlCmd = sqlCon.CreateCommand();
                    sqlCmd.CommandText = deleteQuery;
                    if (queryParameters != null)
                    {
                        foreach (KeyValuePair<object, object> p in queryParameters)
                        {
                            sqlCmd.Parameters.AddWithValue(Convert.ToString(p.Key), p.Value);
                        }
                    }
                    rc = sqlCmd.ExecuteNonQuery();
                    if (rc == 0)
                    {
                        rc = -1;
                    } // end if
                    else
                    {
                        rc = 0;
                    } // end else
                    sqlCmd.Dispose();
                    sqlCon.Dispose();
                }
                else
                {
                    Debug.LogError("Please call SetQuery() before calling Delete().");

                }




            } // end try 
            catch (Exception ex)
            {
                throw ex;
            } // end catch
            return rc;
        } // end method

        public override int Insert(Dictionary<object, object> parameters)
        {
            //
            //Name            : int Insert(object newObj)
            //Purpose         : Try to insert a row in the datastore
            //Re-use          : none
            //Input Parameter : object newObj
            //                  - The object to add to the datastore
            //Output Type     : - int
            //                  0 : newObj inserted into datastore
            //                 -1 : newObj not inserted because a duplicate was found
            //			
            int rc = 0;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;
            try
            {

                //insertQuery = "INSERT INTO "+tableName+"([serialNr], [name], [faction], " +
                //    "[experience]) VALUES(@serialNr, @name, @faction, @experience)";
                if (!query.Equals(""))
                {
                    if (parameters != null)
                    {
                        sqlCon = new SqliteConnection(conStr);
                        sqlCon.Open();
                        sqlCmd = new SqliteCommand(query, sqlCon);
                        foreach (KeyValuePair<object, object> p in parameters)
                        {
                            sqlCmd.Parameters.AddWithValue(Convert.ToString(p.Key), p.Value);
                        }
                        sqlCmd.ExecuteNonQuery();
                        sqlCmd.Dispose();
                        sqlCon.Dispose();
                    }
                    else
                    {
                        Debug.LogError("Please call SetParameters() before calling Insert().");
                    }
                }
                else
                {
                    Debug.LogError("Please call SetQuery() before calling Insert().");

                }



            } // end try
            catch (SqliteException ex)
            {
                if (ex.ErrorCode == SQLiteErrorCode.Constraint)
                {
                    rc = -1;
                } // end if
                else
                {
                    throw ex;
                } // end else
            } // end catch
            catch (Exception ex)
            {
                throw ex;
            } // end catch
            return rc;
        } // end method

        public override List<object> SelectAll()
        {
            //
            //Name            : List<object> SelectAll()
            //Purpose         : Try to get all the objects from the datastore
            //Re-use          : none
            //Input Parameter : None        
            //Output Type     : List<object>
            //                 - the collection that will contain the objects loaded from datastore         
            //
            SqliteConnection sqlCon = null;
            List<object> list;
            SqliteCommand sqlCmd = null;
            SqliteDataReader sqlDr = null;


            try
            {
                list = new List<object>();
                if (!query.Equals(""))
                {
                    sqlCon = new SqliteConnection(conStr);
                    sqlCon.Open();
                    //string selectQuery = "SELECT * FROM Objects";
                    sqlCmd = new SqliteCommand(query, sqlCon);
                    if (queryParameters != null)
                    {
                        foreach (KeyValuePair<object, object> p in queryParameters)
                        {
                            sqlCmd.Parameters.AddWithValue(Convert.ToString(p.Key), p.Value);
                        }
                    }
                    sqlDr = sqlCmd.ExecuteReader();
                    while (sqlDr.Read())
                    {
                        Dictionary<object, object> obj = ConvDataToObject(sqlDr);
                        list.Add(obj);
                    } // end while

                    sqlDr.Close();
                    sqlCmd.Dispose();
                    sqlCon.Dispose();

                }
                else
                {
                    Debug.LogError("Please call SetQuery() before calling SelectAll().");
                }


            } //end try
            catch (Exception ex)
            {
                throw ex;
                //throw ex;
            } // end catch
            return list;
        } // end method

        public override int SelectObject(ref Dictionary<object,object> obj, string serialNr = "")
        {
            //
            //Name            : int SelectObject(string serialNr, ref object obj)
            //Purpose         : Try to get a single object from the datastore
            //Re-use          : none
            //Input Parameter : - string serialNr
            //                   - The serialNr of the object to load from the datastore
            //                  - ref object obj
            //                   - The object loaded from the datastore
            //Output Type     : - int
            //                  0 : object loaded from datastore
            //                 -1 : no object was loaded from the datastore (not found)
            //
            int rc = -1;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;
            SqliteDataReader sqlDr = null;
            bool bFound = false;

            try
            {
                if (!query.Equals(""))
                {
                    sqlCon = new SqliteConnection(conStr);
                    sqlCon.Open();
                    //string selectQuery = string.Format("SELECT * FROM Objects WHERE [serialNr] = '{0}'", serialNr);
                    sqlCmd = new SqliteCommand(query, sqlCon);
                    if (queryParameters != null)
                    {
                        foreach (KeyValuePair<object, object> p in queryParameters)
                        {
                            sqlCmd.Parameters.AddWithValue(Convert.ToString(p.Key), p.Value);
                        }
                    }
                    sqlDr = sqlCmd.ExecuteReader();
                    bFound = sqlDr.Read();
                    if (bFound)
                    {
                        obj = ConvDataToObject(sqlDr);


                        rc = 0;
                    } // end if
                    sqlDr.Close();
                    sqlCmd.Dispose();
                    sqlCon.Dispose();
                }
                else
                {
                    Debug.LogError("Please call SetQuery() before calling SelectObject().");
                }

            } // end try
            catch (Exception ex)
            {
                throw ex;
            } // end catch
            return rc;
        } // end method

        public override int Update(Dictionary<object, object> parameters)
        {
            //
            //Name            : int Update(object obj)
            //Purpose         : Try to update a row in the datastore
            //Re-use          : none
            //Input Parameter : object obj
            //                  - The new object data for the row in the datastore
            //Output Type     : - int
            //                  0 : object found and updated successfully
            //                 -1 : object not updated because the record was not found
            //
            int rc = 0;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;

            try
            {
                if (!query.Equals(""))
                {
                    sqlCon = new SqliteConnection(conStr);
                    sqlCon.Open();

                    //updateQuery = string.Format("UPDATE Objects SET [name] = @name, [faction] = @faction, " +
                    //    "[experience] = @experience WHERE [serialNr] = '{0}'", obj.SerialNr);
                    sqlCmd = new SqliteCommand(query, sqlCon);
                    foreach (KeyValuePair<object, object> p in parameters)
                    {
                        sqlCmd.Parameters.AddWithValue(Convert.ToString(p.Key), p.Value);
                    }
                    rc = sqlCmd.ExecuteNonQuery();
                    if (rc == 0)
                    {
                        rc = -1;
                    } // end if
                    else
                    {
                        rc = 0;
                    } // end else
                    sqlCmd.Dispose();
                    sqlCon.Dispose();
                }
                else
                {
                    Debug.LogError("Please call SetQuery() before calling Update().");
                }

            } // end try
            catch (Exception ex)
            {
                throw ex;
            } // end catch
            return rc;
        } // end method
        #endregion
        #region HELPER/PREREQUISITE METHODS
        public void SetQuery(string query, Dictionary<object, object> parameters = null)
        {
            this.query = query;
            this.queryParameters = parameters;
        }

        
        #endregion

        #region DATABASE MANIPULATION HELPER METHODS
        private bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z0-9_]+$");
        }

        private string EscapeIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return identifier;
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        public int CreateTable(string tableName, List<CandiceColumnInfo> columnInfos)
        {
            if (!IsValidIdentifier(tableName))
            {
                Debug.LogError("Invalid table name provided: " + tableName);
                return -1;
            }

            string columnParameters = "";
            if (columnInfos != null)
            {
                columnParameters = " (";

                for (int i = 0; i < columnInfos.Count; i++)
                {
                    CandiceColumnInfo info = columnInfos[i];
                    if (!IsValidIdentifier(info.Name))
                    {
                        Debug.LogError("Invalid column name provided: " + info.Name);
                        return -1;
                    }
                    if (!IsValidIdentifier(info.Type))
                    {
                        Debug.LogError("Invalid column type provided: " + info.Type);
                        return -1;
                    }

                    string nonNull = "";
                    string autoincrement = "";
                    string pk = "";
                    if (info.Pk)
                    {
                        pk = " PRIMARY KEY";
                    }
                    if (info.Ai)
                    {
                        autoincrement = " AUTOINCREMENT";
                    }
                    if (info.NotNull)
                    {
                        nonNull = " NOT NULL";
                    }

                    string fragment = EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;
                    if (i != columnInfos.Count - 1)
                    {
                        fragment += ", ";
                    }
                    columnParameters += fragment;
                }
                columnParameters += ")";
            }

            int rc = -1;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;
            string createQuery = "";

            try
            {
                sqlCon = new SqliteConnection(conStr);
                sqlCon.Open();
                createQuery = "CREATE TABLE IF NOT EXISTS " + EscapeIdentifier(tableName) + columnParameters;
                Debug.Log(createQuery);

                sqlCmd = new SqliteCommand(createQuery, sqlCon);
                rc = sqlCmd.ExecuteNonQuery();
                sqlCmd.Dispose();
                sqlCon.Dispose();
            }//end try
            catch (Exception ex)
            {
                //throw ex;
                Debug.Log("Datastore Creator_Error: " + ex.Message);
            } // end catch

            return rc;
        }
        public int DeleteTable(string tableName)
        {
            if (!IsValidIdentifier(tableName))
            {
                Debug.LogError("Invalid table name provided: " + tableName);
                return -1;
            }

            int rc = 0;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;
            string createQuery = "";

            try
            {
                sqlCon = new SqliteConnection(conStr);
                sqlCon.Open();
                createQuery = "DROP TABLE IF EXISTS " + EscapeIdentifier(tableName) + ";";
                sqlCmd = new SqliteCommand(createQuery, sqlCon);
                rc = sqlCmd.ExecuteNonQuery();
                sqlCmd.Dispose();
                sqlCon.Dispose();
            }//end try
            catch (Exception ex)
            {
                //throw ex;
                Debug.Log("Datastore Creator_Error: " + ex.Message);
            } // end catch
            return rc;
        }
        public int AddColumn(string tableName, CandiceColumnInfo info)
        {
            if (!IsValidIdentifier(tableName))
            {
                Debug.LogError("Invalid table name provided: " + tableName);
                return -1;
            }
            if (!IsValidIdentifier(info.Name))
            {
                Debug.LogError("Invalid column name provided: " + info.Name);
                return -1;
            }
            if (!IsValidIdentifier(info.Type))
            {
                Debug.LogError("Invalid column type provided: " + info.Type);
                return -1;
            }

            string columnParameters = " ";
            string nonNull = "";
            string autoincrement = "";
            string pk = "";
            if (info.Pk)
            {
                pk = " PRIMARY KEY";
            }
            if (info.Ai)
            {
                autoincrement = " AUTOINCREMENT";
            }
            if (info.NotNull)
            {
                nonNull = " NOT NULL";
            }
            columnParameters += EscapeIdentifier(info.Name) + " " + info.Type + pk + autoincrement + nonNull;


            int rc = 0;
            SqliteConnection sqlCon = null;
            SqliteCommand sqlCmd = null;
            string createQuery = "";

            try
            {
                sqlCon = new SqliteConnection(conStr);
                sqlCon.Open();
                createQuery = "ALTER TABLE " + EscapeIdentifier(tableName) + " ADD COLUMN" + columnParameters;
                sqlCmd = new SqliteCommand(createQuery, sqlCon);
                rc = sqlCmd.ExecuteNonQuery();
                sqlCmd.Dispose();
                sqlCon.Dispose();
            }//end try
            catch (Exception ex)
            {
                //throw ex;
                Debug.Log("Datastore Creator_Error: " + ex.Message);
            } // end catch

            return rc;
        }
        public List<string> GetTableNames()
        {
            //
            //Name            : List<string> GetTableNames()
            //Purpose         : Try to get all the table names from the current database
            //Re-use          : none
            //Input Parameter : None        
            //Output Type     : List<string>
            //                 - the string collection that will contain the names of all tables from the database         
            //
            SqliteConnection sqlCon = null;
            List<string> list;
            SqliteCommand sqlCmd = null;
            SqliteDataReader sqlDr = null;


            try
            {
                list = new List<string>();

                sqlCon = new SqliteConnection(conStr);
                sqlCon.Open();
                string selectQuery = "SELECT name FROM sqlite_master WHERE type='table';";
                sqlCmd = new SqliteCommand(selectQuery, sqlCon);
                sqlDr = sqlCmd.ExecuteReader();
                while (sqlDr.Read())
                {
                    string tableName = Convert.ToString(sqlDr["name"]);
                    list.Add(tableName);
                } // end while
                sqlDr.Close();
                sqlCmd.Dispose();
                sqlCon.Dispose();
            } //end try
            catch (Exception ex)
            {
                throw ex;
                //throw ex;
            } // end catch
            return list;
        } // end method

        public List<CandiceColumnInfo> GetColumnInfo(string tableName)
        {
            if (!IsValidIdentifier(tableName))
            {
                Debug.LogError("Invalid table name provided: " + tableName);
                return new List<CandiceColumnInfo>();
            }

            //
            //Name            : List<ColumnInfo> GetColumnInfo()
            //Purpose         : Try to get all the Column information from the table
            //Re-use          : none
            //Input Parameter : string tableName        
            //Output Type     : List<ColumnInfo>
            //                 - the ColumnInfo collection that will contain the column information from the specified table         
            //
            SqliteConnection sqlCon = null;
            List<CandiceColumnInfo> listColumnInfo = new List<CandiceColumnInfo>();
            SqliteCommand sqlCmd = null;
            SqliteDataReader sqlDr = null;


            try
            {

                sqlCon = new SqliteConnection(conStr);
                sqlCon.Open();
                string selectQuery = "PRAGMA table_info(" + EscapeIdentifier(tableName) + "); ";
                sqlCmd = new SqliteCommand(selectQuery, sqlCon);
                sqlDr = sqlCmd.ExecuteReader();
                while (sqlDr.Read())
                {
                    string name = Convert.ToString(sqlDr["name"]);
                    string type = Convert.ToString(sqlDr["type"]);
                    string defaultValue = Convert.ToString(sqlDr["dflt_value"]);
                    bool notNull = Convert.ToBoolean(sqlDr["notnull"]);
                    bool pk = Convert.ToBoolean(sqlDr["pk"]);
                    bool ai = false;
                    //bool ai = Convert.ToBoolean(sqlDr["auto"]);
                    CandiceColumnInfo columnInfo = new CandiceColumnInfo(name, type, defaultValue, notNull, pk, ai);
                    listColumnInfo.Add(columnInfo);
                } // end while
                sqlDr.Close();
                sqlCmd.Dispose();
                sqlCon.Dispose();
            } //end try
            catch (Exception ex)
            {
                throw ex;
                //throw ex;
            } // end catch
            return listColumnInfo;
        }

        public void ChangeConnectionString(string conStr)
        {
            this.conStr = conStr;
        }
        #endregion

        private Dictionary<object,object> ConvDataToObject(SqliteDataReader sqlDr)
        {
            //
            //Name            : Dictionary<string,string> ConvDataToObject(SqliteDataReader sqlDr)
            //Purpose         : convert the data stream into a Dictionary object
            //Re-use          : none
            //Input Parameter : SqliteDataReader sqlDr
            //                   - the data reader containing the stream of data to convert
            //Output Type     : - Weapon
            //                 The object that will be used by the user
            //
            Dictionary<object, object> obj = new Dictionary<object, object>();
            try
            {
                for (int i = 0; i < sqlDr.FieldCount; i++)
                {
                    string column = sqlDr.GetName(i);
                    obj.Add(column, Convert.ToString(sqlDr[column]));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("ERROR ConvDataToWeapon(): " + e.Message);
            }


            return obj;
        }
    }
#else
    public class CandiceSQLiteProvider : CandiceProviderBase
    {
        private const int ProviderUnavailable = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string DisabledMessage = "Candice SQLite provider disabled: legacy Mono.Data.Sqlite is quarantined. Define CANDICE_LEGACY_MONO_SQLITE only when the platform supplies compatible SQLite binaries.";
        private static bool s_loggedDisabled;
#endif

        // COLD ALLOC: List<object>[0] - disabled provider empty result cache - owner: CandiceSQLiteProvider
        private static readonly List<object> EmptyObjects = new List<object>(0);

        // COLD ALLOC: List<string>[0] - disabled provider empty table-name cache - owner: CandiceSQLiteProvider
        private static readonly List<string> EmptyStrings = new List<string>(0);

        // COLD ALLOC: List<CandiceColumnInfo>[0] - disabled provider empty column-info cache - owner: CandiceSQLiteProvider
        private static readonly List<CandiceColumnInfo> EmptyColumnInfos = new List<CandiceColumnInfo>(0);

        public CandiceSQLiteProvider(string conStr)
        {
        }

        public override int Delete(string serialNr = "")
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public override int Insert(Dictionary<object, object> parameters)
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public override List<object> SelectAll()
        {
            LogDisabledOnce();
            EmptyObjects.Clear();
            return EmptyObjects;
        }

        public override int SelectObject(ref Dictionary<object, object> obj, string serialNr = "")
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public override int Update(Dictionary<object, object> parameters)
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public void SetQuery(string query, Dictionary<object, object> parameters = null)
        {
        }

        public int CreateTable(string tableName, List<CandiceColumnInfo> columnInfos)
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public int DeleteTable(string tableName)
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public int AddColumn(string tableName, CandiceColumnInfo info)
        {
            LogDisabledOnce();
            return ProviderUnavailable;
        }

        public List<string> GetTableNames()
        {
            LogDisabledOnce();
            EmptyStrings.Clear();
            return EmptyStrings;
        }

        public List<CandiceColumnInfo> GetColumnInfo(string tableName)
        {
            LogDisabledOnce();
            EmptyColumnInfos.Clear();
            return EmptyColumnInfos;
        }

        public void ChangeConnectionString(string conStr)
        {
        }

        private static void LogDisabledOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (s_loggedDisabled)
            {
                return;
            }

            s_loggedDisabled = true;
            Debug.LogWarning(DisabledMessage);
#endif
        }
    }
#endif
}

