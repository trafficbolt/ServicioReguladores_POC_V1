using EasyModbus;
using POCServicioReguladores.Enums;
using POCServicioReguladores.EventLogPOC;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace POCServicioReguladores
{

    partial class MR : ServiceBase
    {
        #region DeclaracionVariables
        private static string conexionstring = "Server= DESKTOP-QV9DU6V\\SQLEXPRESS; Database= AIFA-DATA-MANAGEMENT; Integrated Security = SSPI; ";
        //static string conexionstring = "Server=192.168.3.215;Database=AIFA-DATA-MANAGEMENT;user id =sa;password=AIFA2020";
        private SqlConnection conexion = new SqlConnection(conexionstring);
        private List<string> ipsToUpdateAsync;
        private System.Timers.Timer oTimer;        
        private bool bInicioRecorrido = false;
        private int destellos = 0;
        #endregion        

        protected override void OnStart(string[] args)
        {
            try
            {                
                Log.WriteToFile($"1. Service is started at: { DateTime.Now }");
                oTimer = new System.Timers.Timer(1500)
                {
                    Enabled = true,
                    Interval = Convert.ToInt32(ConfigurationManager.AppSettings["intervalosEjecucion"])
                };
                oTimer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
                oTimer.Start();                
            }
            catch (Exception ex)
            {
                Log.WriteToFile($"OnStar | Exception: { ex.Message }");
            }
        }

        protected override void OnStop()
        {            
            Log.WriteToFile($"Service is stopped at: { DateTime.Now }");
        }               

        private Dictionary<string, string> GetIPs()
        {
            Dictionary<string, string> ips = new Dictionary<string, string>();
            try
            {
                conexion.Open();
                string q = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED SELECT nomenclatura, ip FROM sw_estados_reguladores WITH (NOLOCK)";
                SqlCommand comando = new SqlCommand(q, conexion);
                SqlDataAdapter data = new SqlDataAdapter(comando);
                DataTable tabla = new DataTable();
                data.Fill(tabla);
                foreach(DataRow dr in tabla.Rows)
                {
                    ips.Add(dr[0].ToString(), dr[1].ToString());
                }
            }
            catch(Exception ex)
            {
                Log.WriteToFile($"GetIPs | Exception: { ex.Message }");
            }
            finally
            {
                conexion.Close();
            }
            return ips;
        }

        private Dictionary<int, Tuple<string, string>> GetIds()
        {
            Dictionary<int, Tuple<string, string>> idsResult = new Dictionary<int, Tuple<string, string>>();            
            var ids = new List<string>();
            try
            {
                conexion.Open();
                string q = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED SELECT idBoton FROM sw_estados_reguladores WITH (NOLOCK)";
                SqlCommand comando = new SqlCommand(q, conexion);
                SqlDataAdapter data = new SqlDataAdapter(comando);
                DataTable tabla = new DataTable();
                data.Fill(tabla);                
                foreach (DataRow dr in tabla.Rows)
                {
                    ids.Add(dr[0].ToString());
                }

                ids.ForEach(id =>
                {
                    switch (id)
                    {
                        case "APROXIMACIONES NORTE 04L": idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_APROXIMACION")); break;
                        case "PAPI 04L":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_PAPI")); break;
                        case "FLASH 04L":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_Destellos")); break;
                        case "APROXIMACIONES NORTE 22R":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_APROXIMACION")); break;
                        case "PAPI 22R":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_PAPI")); break;
                        case "FLASH 22R":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_Destellos")); break;
                        case "TDZ 04L": ; idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_TDZ")); break;
                        case "BORDE DE PISTA NORTE":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_BORDES")); break;
                        case "EJE DE PISTA NORTE":  idsResult.Add(PistaEnum.NORTE.GetHashCode(), Tuple.Create(id, "ID_EJE")); break;

                        case "APROXIMACIONES 04C": idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_APROXIMACION")); break;
                        case "PAPI 04C":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_PAPI")); break;
                        case "FLASH 04C":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_Destellos")); break;
                        case "APROXIMACIONES 22C":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_APROXIMACION")); break;
                        case "PAPI 22C":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_PAPI")); break;
                        case "FLASH 22C":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_Destellos")); break;
                        case "TDZ 04C":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_TDZ")); break;
                        case "BORDE DE PISTA CENTRO":  idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_BORDES")); break;
                        case "EJE DE PISTA CENTRO": idsResult.Add(PistaEnum.CENTRAL.GetHashCode(), Tuple.Create(id, "ID_EJE")); break;

                        case "BARRAS DE PARADA K04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_BARRAS_K2_K7")); break;
                        case "BARRAS DE PARADA PQ-04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BARRAS_DE_PARADA_PQ04C")); break;
                        case "BARRAS DE PARADA K22C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_BARRAS_K11_K17")); break;
                        case "BARRAS DE PARADA A04L":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_BARRAS_A1_A6")); break;
                        case "BARRAS DE PARADA PQ04L":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BARRAS_DE_PARADA_PQ04L")); break;
                        case "BARRAS DE PARADA A22R":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_BARRAS_A8_A15")); break;
                        case "EJE A04L":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_A04L")); break;
                        case "EJE A04L2":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_A04L2")); break;
                        case "EJE APQ 22R":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_APQ22R")); break;
                        case "EJE AB 22R":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_AB22R")); break;
                        case "EJE B04L":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_B04L")); break;
                        case "EJE B04L2":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_B04L2")); break;
                        case "EJE BPQ22R":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_BPQ22r")); break;
                        case "EJE CC":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_CC")); break;
                        case "EJE EF04L":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EF04L")); break;
                        case "EJE FE04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_FE04C")); break;
                        case "EJE HK04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_HK04C")); break;
                        case "EJE HJ04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_HJ04C")); break;
                        case "EJE J04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_J04C")); break;
                        case "EJE JPQ04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_JPQ04C")); break;
                        case "EJE JJ22C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_JJ22C")); break;
                        case "EJE J22C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_J22C")); break;
                        case "EJE K04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_K04C")); break;
                        case "EJE KPQ04C22C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_KPQ04C22C")); break;
                        case "EJE KPQ04C22C2":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_KPQ04C22C2")); break;
                        case "EJE K22C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_K22C")); break;
                        case "EJE PQ22R":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_PQ22R"));break;
                        case "EJE PQ04C":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_PQ04C")); break;
                        case "BORDES NORTE":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_BORDES_NORTE")); break;
                        case "BORDES CENTRO":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_BORDES_CENTRAL")); break;
                        case "LETREROS NORTE":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_LETREROS_NORTE")); break;
                        case "LETREROS CENTRO":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_LERTREROS_CENTRAL")); break;
                        case "EJE Z":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_Z")); break;
                        case "TODOS LOS ATRAQUES":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_ATRAQUES")); break;
                        case "EJE B22R":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_EJE_B22R")); break;
                        case "EJE SALIDA RAPIDAS NORTE RETA6":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_RET_A6")); break;
                        case "EJE SALIDA RAPIDAS NORTE RETA8":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_RET_A8")); break;
                        case "EJE SALIDA RAPIDAS NORTE TLO A1-A5":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_TLO_A1_A5")); break;
                        case "EJE SALIDA RAPIDAS NORTE TLO A9-A15":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_TLO_A9_A15")); break;
                        case "EJE SALIDA RAPIDAS CENTRO RET K7":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_RET_K7")); break;
                        case "EJE SALIDA RAPIDAS CENTRO RET K11":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_RET_K11")); break;
                        case "EJE SALIDA RAPIDAS CENTRO TLO K2-K6":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_TLO_K2_K6")); break;
                        case "EJE SALIDA RAPIDAS CENTRO TLO K14-K17":  idsResult.Add(PistaEnum.RODAJES.GetHashCode(), Tuple.Create(id, "ID_BOTON_SALIDAS_TLO_K14_K17")); break;
                        default: break;
                    }                                                                
                });
            }
            catch (Exception ex)
            {
                Log.WriteToFile($"GetIds | Exception: { ex.Message }");
            }
            finally
            {
                conexion.Close();
            }
            return idsResult;
        }

        private string Consulta_Comunicacion(ModbusClient modbusClient, string pIp, string pNomenclatura)
        {
            int[] response = null;
            int[] brilloactual = null;
            int[] comandobrillo = null;
            var status0 = String.Empty;
            var binary = String.Empty;
            Thread.Sleep(100);

            try
            {                
                try
                {                    
                    modbusClient.Connect();
                    
                    // Lectura de reguladores
                    if (destellos == 0)
                    {
                        if (modbusClient.Connected)
                        {
                            for (int i = 0; i <= 10; i++)
                            {
                                response = modbusClient.ReadHoldingRegisters(0, 1);
                                Thread.Sleep(75);
                                brilloactual = modbusClient.ReadHoldingRegisters(100, 1);
                                Thread.Sleep(75);
                                comandobrillo = modbusClient.ReadHoldingRegisters(1, 1);
                                Thread.Sleep(75);
                            }
                            binary = Convert.ToString(response[0], 2);
                            var ch = new char[binary.Length];
                            for (int i = 0; i < binary.Length; i++)
                            {
                                ch[i] = binary[i];
                            }
                            //  Local
                            if (response[0] == 1)
                            {
                                status0 = "3";
                            }
                            //  alarmado
                            else if (ch[3] == 1 || ch[5] == 1 || (ch.Length == 13 && ch[13] == 1))
                            {
                                status0 = "6";
                            }
                            //  alarmado
                            else if (ch[3] == 1)
                            {
                                status0 = "6";
                            }
                            //  alarmado
                            else if (ch[5] == 1)
                            {
                                status0 = "6";
                            }
                            else if (response[0] == 153 || response[0] == 152)
                            {
                                status0 = "6";
                            }
                            //  OK
                            else if (ch[3] != 1 && ch[5] != 1 && (ch.Length == 13 && ch[13] != 1))
                            {
                                status0 = "-1";
                                //  No alarmado - Trabado
                                if (brilloactual[0] != comandobrillo[0])
                                {
                                    status0 = "4";
                                }
                            }
                            //  OK
                            else if (ch[3] != 1 && ch[5] != 1)
                            {
                                status0 = "-1";
                                //  No alarmado - Trabado
                                if (brilloactual[0] != comandobrillo[0])
                                {
                                    status0 = "4";
                                }
                            }
                            //  inalcanzable
                            else if (response.Length == 0 || response.Length <= 0 || response == null)
                            {
                                status0 = "6";
                            }
                            //  OK
                            else if (response[0] == 128 || response[0] == 129)
                            {
                                status0 = "-1";
                                //  No alarmado - Trabado
                                if (brilloactual[0] != comandobrillo[0])
                                {
                                    status0 = "4";
                                }
                            }
                            else if (response[0] == 1669 || response[0] == 1668)
                            {
                                status0 = "-1";
                            }
                            else
                            {
                                status0 = "-2";
                            }
                        }
                        //  inalcanzable
                        else
                        {
                            status0 = "6";
                        }

                    }
                    //Lectura de destellos OCEM
                    else if (destellos == 1)
                    {
                        if (modbusClient.Connected)
                        {
                            for (int i = 0; i <= 10; i++)
                            {
                                response = modbusClient.ReadHoldingRegisters(2, 1);
                                Thread.Sleep(150);
                            }
                            //  inalcanzable
                            if (response.Length == 0 || response.Length <= 0 || response == null)
                            {
                                status0 = "6";
                            }
                            //  OK
                            else if (response[0] < 63)
                            {
                                status0 = "-1";
                            }
                            //Alarmado
                            else if (response[0] > 63)
                            {
                                status0 = "6";
                            }
                        }
                        //  inalcanzable
                        else
                        {
                            status0 = "6";
                        }
                    }

                }
                catch (Exception)
                {
                    status0 = "5";
                }
            }
            catch (Exception ex)
            {
                Log.WriteToFile($"Consulta_Comunicacion | Exception: { ex.Message },\nIP: { pIp } \nNomenclatura: { pNomenclatura }  \nAt: { DateTime.Now }");
            }
            finally
            {
                modbusClient.Disconnect();
            }

            return $"Update sw_estados_reguladores set ip = '{ pIp }', status = '{ status0 }' Where nomenclatura = '{ pNomenclatura }' ";
        }

        private void ActualizaIpsAsync()
        {            
            try
            {
                conexion.Open();
                ipsToUpdateAsync.ForEach(query =>
                {
                    if (!String.IsNullOrEmpty(query))
                    {
                        SqlCommand cmd = new SqlCommand(query, conexion);
                        cmd.ExecuteNonQuery();
                    }                    
                });
            }
            catch(Exception ex)
            {
                Log.WriteToFile($"ActualizaIpsAsync | Exception: { ex.Message }");
            }
            finally
            {
                conexion.Close();
                ipsToUpdateAsync = new List<string>();
            }
        }        

        private int GetStatus(string query)
        {
            int result = 0;

            try
            {
                conexion.Open();

                SqlCommand comando = new SqlCommand(query, conexion);
                SqlDataAdapter data = new SqlDataAdapter(comando);
                DataTable tabla = new DataTable();
                data.Fill(tabla);                

                foreach (DataRow dgvR in tabla.Rows)
                {
                    for (int j = 0; j < tabla.Columns.Count; ++j)
                    {
                        object val = dgvR[j];
                        if (val != null)
                        {
                            result = int.Parse(val.ToString());
                        }                        
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteToFile($"Consulta | Exception: { ex.Message }");
            }
            finally
            {
                conexion.Close();
            }

            return result;
        }

        private void Actualiza_Tablas_Norte(string idBoton, string status)
        {
            try
            {                
                conexion.Open();
                var query = "Update sw_estados_norte set status = '" + status + "' Where idBoton = '" + idBoton + "'";
                SqlCommand comando2 = new SqlCommand(query, conexion);
                comando2.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Log.WriteToFile($"Actualiza_Tablas_Norte | Exception: { ex.Message }, at { DateTime.Now }");
            }
            finally
            {
                conexion.Close();
            }            
        }

        private void Actualiza_Tablas_Centro(string idBoton, string status)
        {
            try
            {
                conexion.Open();
                var query = "Update sw_estados_central set idBoton = '" + idBoton + "', status = '" + status + "' Where idBoton = '" + idBoton + "'";
                SqlCommand comando3 = new SqlCommand(query, conexion);
                comando3.ExecuteNonQuery();                               
            }    
            catch(Exception ex)
            {
                Log.WriteToFile($"Actualiza_Tablas_Centro | Exception: { ex.Message }, at { DateTime.Now }");
            }
            finally
            {
                conexion.Close();
            }
        }

        private void Actualiza_Tablas_Rodajes(string idBoton, string status)
        {
            try
            {
                conexion.Open();
                var query = "Update sw_estados_rodajes set idBoton = '" + idBoton + "', status = '" + status + "' Where idBoton = '" + idBoton + "'";
                SqlCommand comando4 = new SqlCommand(query, conexion);
                comando4.ExecuteNonQuery();              
            }
            catch(Exception ex)
            {
                Log.WriteToFile($"Actualiza_Tablas_Rodajes | Exception: { ex.Message }, at { DateTime.Now }");
            }   
            finally
            {
                conexion.Close();
            }
        }

        private void Cambia_estados_norte(int estadoNorte, string idBoton)
        {
            //  Timeout - Alarmado
            if (estadoNorte == 6)
            {
                var status = ReguladorEnum.TIME_OUT.GetHashCode().ToString();
                Actualiza_Tablas_Norte(idBoton, status);
            }
            //  Inalcanzable
            else if (estadoNorte == 5 || estadoNorte == 0)
            {
                var status = ReguladorEnum.INALCANZABLE.GetHashCode().ToString();
                Actualiza_Tablas_Norte(idBoton, status);
            }
            //  No alarmado - Trabado
            else if (estadoNorte == 4)
            {
                var status = ReguladorEnum.NO_ALARMADO.GetHashCode().ToString();
                Actualiza_Tablas_Norte(idBoton, status);
            }
            //  Local
            else if (estadoNorte == 3)
            {
                var status = ReguladorEnum.LOCAL.GetHashCode().ToString();
                Actualiza_Tablas_Norte(idBoton, status);
            }
            //  Mantenimiento
            else if (estadoNorte == 2)
            {
                var status = ReguladorEnum.MANTENIMIENTO.GetHashCode().ToString();
                Actualiza_Tablas_Norte(idBoton, status);
            }
            //  Correcto
            else if (estadoNorte == -1)
            {
                var status = ReguladorEnum.CORRECTO.GetHashCode().ToString();
                Actualiza_Tablas_Norte(idBoton, status);
            }
        }

        private void Cambia_estados_centro(int estadoCentro, string IDBOTON)
        {
            //  Timeout - Alarmado
            if (estadoCentro == 6)
            {
                var status = "3";
                Actualiza_Tablas_Centro(IDBOTON, status);
            }
            //  Inalcanzable
            else if (estadoCentro == 5 || estadoCentro == 0)
            {
                var status = "4";
                Actualiza_Tablas_Centro(IDBOTON, status);
            }
            //  No alarmado - Trabado
            else if (estadoCentro == 4)
            {
                var status = "5";
                Actualiza_Tablas_Centro(IDBOTON, status);
            }
            //  Local
            else if (estadoCentro == 3)
            {
                var status = "2";
                Actualiza_Tablas_Centro(IDBOTON, status);
            }
            //  Mantenimiento
            else if (estadoCentro == 2)
            {
                var status = "6";
                Actualiza_Tablas_Centro(IDBOTON, status);
            }
            //  Correcto
            else if (estadoCentro == -1)
            {
                var status = "1";
                Actualiza_Tablas_Centro(IDBOTON, status);
            }
        }

        private void Cambia_estados_rodajes(int estadoRodaje, string IDBOTON)
        {
            //  Timeout - Alarmado
            if (estadoRodaje == 6)
            {
                var status = "3";
                Actualiza_Tablas_Rodajes(IDBOTON, status);
            }
            //  Inalcanzable
            else if (estadoRodaje == 5 || estadoRodaje == 0)
            {
                var status = "4";
                Actualiza_Tablas_Rodajes(IDBOTON, status);
            }
            //  No alarmado - Trabado
            else if (estadoRodaje == 4)
            {
                var status = "5";
                Actualiza_Tablas_Rodajes(IDBOTON, status);
            }
            //  Local
            else if (estadoRodaje == 3)
            {
                var status = "2";
                Actualiza_Tablas_Rodajes(IDBOTON, status);
            }
            //  Mantenimiento
            else if (estadoRodaje == 2)
            {
                var status = "6";
                Actualiza_Tablas_Rodajes(IDBOTON, status);
            }
            //  Correcto
            else if (estadoRodaje == -1)
            {
                var status = "1";
                Actualiza_Tablas_Rodajes(IDBOTON, status);
            }
        }

        private DataTable ConsultaCabecera()
        {
            DataTable tabla = new DataTable();

            try
            {                                
                conexion.Open();
                SqlCommand comando = new SqlCommand("Select Tipo, Cabecera from cabecera_activa where Activa = 'True'", conexion);
                SqlDataAdapter data = new SqlDataAdapter(comando);
                
                data.Fill(tabla);
            }
            catch(Exception ex)
            {
                Log.WriteToFile($"ConsultaCabecera | Exception: { ex.Message }");
            }
            finally
            {
                conexion.Close();                
            }

            return tabla;
        }

        private void UpdateStatus(PistaEnum pista, string idBoton, string idBotonUI)
        {
            var query = $"select max(status) from sw_estados_reguladores where idBoton='{ idBoton }'";
            var status = GetStatus(query);

            switch (pista)
            {
                case PistaEnum.NORTE:
                    Cambia_estados_norte(status, idBotonUI);
                    break;
                case PistaEnum.CENTRAL:
                    Cambia_estados_centro(status, idBotonUI);
                    break;
                case PistaEnum.RODAJES:
                    Cambia_estados_rodajes(status, idBotonUI);
                    break;
            }            
        }
        
        async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (bInicioRecorrido)
                return;

            try
            {                
                bInicioRecorrido = true;                

                Debugger.Launch();

                Log.WriteToFile($"2. Processing Modbus Client...");
                ipsToUpdateAsync = new List<string>();

                var tasks = new List<Task>();

                Log.WriteToFile($"3. Get IP's...");
                var ips = GetIPs();
                
                ips.ToList().ForEach(obj =>
                {
                    var t = new Task<string>(() =>
                    {
                        var modbusClient = new ModbusClient(obj.Value, 502);
                        var result = Consulta_Comunicacion(modbusClient, obj.Value, obj.Key);
                        return result;
                    });
                        
                    t.Start();
                    tasks.Add(t);                        
                });                                
                    
                var tasksToAwait = Task.WhenAll(tasks);
                try
                {
                    await tasksToAwait;
                    tasks.ForEach(x => {
                        var query = ((Task<string>)x).Result;
                        ipsToUpdateAsync.Add(query);
                    });                                                 
                }
                catch
                {
                    Log.WriteToFile($"**** Await tasks exception: { tasksToAwait.Exception }");
                }                
                
                Log.WriteToFile($"4. Modbus Client processed successfully at { DateTime.Now }");
                
                Log.WriteToFile($"5. Updating data. Length: { ipsToUpdateAsync.Count }");
                ActualizaIpsAsync();

                Debugger.Launch();

                Log.WriteToFile($"6. Getting header data...");
                var cabeceraResult = ConsultaCabecera();
                var cabeceraActivaNorte = cabeceraResult.Rows[0]["Cabecera"].ToString();
                var cabeceraActivaCentral = cabeceraResult.Rows[1]["Cabecera"].ToString();

                Log.WriteToFile($"7. Getting button's id...");
                var idButtons = GetIds();                

                Log.WriteToFile($"8. Updating states...");
                idButtons.ToList().ForEach(x => {
                    var data = x.Value;                    
                    UpdateStatus((PistaEnum)x.Key, x.Value.Item1, x.Value.Item2);
                });               
            }
            catch (Exception ex)
            {
                Log.WriteToFile($"Timer_elapsed | exception:  { ex.Message }");
                bInicioRecorrido = false;
            }
            
            Log.WriteToFile($"6. Service has ended successfully at : { DateTime.Now } \n\n");
            bInicioRecorrido = false;                            
        }
 
    }
}
