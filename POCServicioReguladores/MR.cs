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
        static string conexionstring = "Server= DESKTOP-QV9DU6V\\SQLEXPRESS; Database= AIFA-DATA-MANAGEMENT; Integrated Security = SSPI; ";
        //static string conexionstring = "Server=192.168.3.215;Database=AIFA-DATA-MANAGEMENT;user id =sa;password=AIFA2020";
        SqlConnection conexion = new SqlConnection(conexionstring);

        List<string> ipsToUpdateAsync;

        System.Timers.Timer oTimer;

        string query1;
        string query2;
        string query3;
        string cadena1;
        string IDBOTON;
        string STATUS;        
        string valorObtenido;
        string Cabecera_activa_norte;
        string Cabecera_activa_centro;

        int contador = 1;
        int valor;
        int estadoNorte;
        int estadoCentro;
        int estadoRodaje;
        int flag;
        int destellos = 0;

        bool bInicioRecorrido = false;
        DataTable datos = new DataTable();

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


        #region Metodos
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

                //Actualiza_BD_1(pIp, status0, pNomenclatura, new SqlConnection(conexionstring));
                //UpdateQueryIpsAsync(pIp, status0, pNomenclatura);
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

        private void UpdateQueryIpsAsync(string pIp, string pStatus, string pNomenclatura)
        {
            try
            {
                var query = $"Update sw_estados_reguladores set ip = '{ pIp }', status = '{ pStatus }' Where nomenclatura = '{ pNomenclatura }' ";
                ipsToUpdateAsync.Add(query);
            }
            catch(Exception ex)
            {
                Log.WriteToFile($"UpdateQueryIpsAsync | Exception: { ex.Message }");
            }            
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
        private void Actualiza_BD_1(string pIp, string pStatus, string pNomenclatura, SqlConnection conexionBD)
        {
            try
            {
                conexionBD.Open();
                flag = 0;                
                cadena1 = "Update sw_estados_reguladores set ip = '" + pIp + "', status = '" + pStatus + "' Where nomenclatura = '" + pNomenclatura + "'";
                SqlCommand comando1 = new SqlCommand(cadena1, conexionBD);
                comando1.ExecuteNonQuery();                                
            }
            catch(Exception ex)
            {
                Log.WriteToFile($"Actualiza_BD_1 | Exception:  { ex.Message}, at: { DateTime.Now }");
            }
            finally
            {
                conexionBD.Close();
            }
        }

        private int Consulta(string query)
        {
            try
            {
                conexion.Open();

                SqlCommand comando = new SqlCommand(query, conexion);
                SqlDataAdapter data = new SqlDataAdapter(comando);
                DataTable tabla = new DataTable();
                data.Fill(tabla);
                conexion.Close();

                foreach (DataRow dgvR in tabla.Rows)
                {
                    for (int j = 0; j < tabla.Columns.Count; ++j)
                    {
                        object val = dgvR[j];
                        if (val == null)
                        {

                        }
                        else
                        {
                            valorObtenido = val.ToString();
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            valor = Convert.ToInt32(valorObtenido);
            return valor;
        }

        private void Actualiza_Tablas_Norte(string idBoton, string status)
        {
            try
            {
                flag = 0;
                conexion.Open();
                query3 = "Update sw_estados_norte set status = '" + status + "' Where idBoton = '" + idBoton + "'";
                SqlCommand comando2 = new SqlCommand(query3, conexion);
                flag = comando2.ExecuteNonQuery();
                conexion.Close();

                if (flag == 1)
                {

                }
                else
                {

                }
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
                flag = 0;
                conexion.Open();
                query3 = "Update sw_estados_central set idBoton = '" + idBoton + "', status = '" + status + "' Where idBoton = '" + idBoton + "'";
                SqlCommand comando3 = new SqlCommand(query3, conexion);
                flag = comando3.ExecuteNonQuery();               
                if (flag == 1)
                {

                }
                else
                {

                }
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
                flag = 0;
                conexion.Open();
                query3 = "Update sw_estados_rodajes set idBoton = '" + idBoton + "', status = '" + status + "' Where idBoton = '" + idBoton + "'";
                SqlCommand comando4 = new SqlCommand(query3, conexion);
                flag = comando4.ExecuteNonQuery();
                
                if (flag == 1)
                {

                }
                else
                {

                }
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

        private void Cambia_estados_norte(int estadoNorte, string IDBOTON)
        {
            //  Timeout - Alarmado
            if (estadoNorte == 6)
            {
                STATUS = ReguladorEnum.TIME_OUT.GetHashCode().ToString();
                Actualiza_Tablas_Norte(IDBOTON, STATUS);
            }
            //  Inalcanzable
            else if (estadoNorte == 5 || estadoNorte == 0)
            {
                STATUS = ReguladorEnum.INALCANZABLE.GetHashCode().ToString();
                Actualiza_Tablas_Norte(IDBOTON, STATUS);
            }
            //  No alarmado - Trabado
            else if (estadoNorte == 4)
            {
                STATUS = ReguladorEnum.NO_ALARMADO.GetHashCode().ToString();
                Actualiza_Tablas_Norte(IDBOTON, STATUS);
            }
            //  Local
            else if (estadoNorte == 3)
            {
                STATUS = ReguladorEnum.LOCAL.GetHashCode().ToString();
                Actualiza_Tablas_Norte(IDBOTON, STATUS);
            }
            //  Mantenimiento
            else if (estadoNorte == 2)
            {
                STATUS = ReguladorEnum.MANTENIMIENTO.GetHashCode().ToString();
                Actualiza_Tablas_Norte(IDBOTON, STATUS);
            }
            //  Correcto
            else if (estadoNorte == -1)
            {
                STATUS = ReguladorEnum.CORRECTO.GetHashCode().ToString();
                Actualiza_Tablas_Norte(IDBOTON, STATUS);
            }
        }

        private void Cambia_estados_centro(int estadoCentro, string IDBOTON)
        {
            //  Timeout - Alarmado
            if (estadoCentro == 6)
            {
                STATUS = "3";
                Actualiza_Tablas_Centro(IDBOTON, STATUS);
            }
            //  Inalcanzable
            else if (estadoCentro == 5 || estadoCentro == 0)
            {
                STATUS = "4";
                Actualiza_Tablas_Centro(IDBOTON, STATUS);
            }
            //  No alarmado - Trabado
            else if (estadoCentro == 4)
            {
                STATUS = "5";
                Actualiza_Tablas_Centro(IDBOTON, STATUS);
            }
            //  Local
            else if (estadoCentro == 3)
            {
                STATUS = "2";
                Actualiza_Tablas_Centro(IDBOTON, STATUS);
            }
            //  Mantenimiento
            else if (estadoCentro == 2)
            {
                STATUS = "6";
                Actualiza_Tablas_Centro(IDBOTON, STATUS);
            }
            //  Correcto
            else if (estadoCentro == -1)
            {
                STATUS = "1";
                Actualiza_Tablas_Centro(IDBOTON, STATUS);
            }
        }

        private void Cambia_estados_rodajes(int estadoRodaje, string IDBOTON)
        {
            //  Timeout - Alarmado
            if (estadoRodaje == 6)
            {
                STATUS = "3";
                Actualiza_Tablas_Rodajes(IDBOTON, STATUS);
            }
            //  Inalcanzable
            else if (estadoRodaje == 5 || estadoRodaje == 0)
            {
                STATUS = "4";
                Actualiza_Tablas_Rodajes(IDBOTON, STATUS);
            }
            //  No alarmado - Trabado
            else if (estadoRodaje == 4)
            {
                STATUS = "5";
                Actualiza_Tablas_Rodajes(IDBOTON, STATUS);
            }
            //  Local
            else if (estadoRodaje == 3)
            {
                STATUS = "2";
                Actualiza_Tablas_Rodajes(IDBOTON, STATUS);
            }
            //  Mantenimiento
            else if (estadoRodaje == 2)
            {
                STATUS = "6";
                Actualiza_Tablas_Rodajes(IDBOTON, STATUS);
            }
            //  Correcto
            else if (estadoRodaje == -1)
            {
                STATUS = "1";
                Actualiza_Tablas_Rodajes(IDBOTON, STATUS);
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

        #endregion      
        
        async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {

            try
            {
                if (bInicioRecorrido)
                    return;

                bInicioRecorrido = true;

                //datos = ConsultaCabecera();

                //Cabecera_activa_norte = datos.Rows[0]["Cabecera"].ToString();
                //Cabecera_activa_centro = datos.Rows[1]["Cabecera"].ToString();

                //destellos = 0;

                Debugger.Launch();

                Log.WriteToFile($"2. Processing Modbus Client...");
                ipsToUpdateAsync = new List<string>();

                var tasks = new List<Task>();

                Log.WriteToFile($"3. Get IP's...");
                var ips = GetIPs();

                if(ips.Count == 164)
                {
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
                } else
                {
                    Log.WriteToFile($"**** GetIPs method did not get all ips");
                }
                

                if(ips.Count == tasks.Count)
                {
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
                }
                else
                {
                    Log.WriteToFile($"**** The ips length and tasks are not equal");
                }
                
                Log.WriteToFile($"4. Modbus Client processed successfully at { DateTime.Now }");
                
                Log.WriteToFile($"5. Updating data. Length: { ipsToUpdateAsync.Count }");
                ActualizaIpsAsync();

                //#region Norte
                ////Pista norte
                //if (contador == 169)
                //{
                //    if (Cabecera_activa_norte == "idCabeceraL")
                //    {
                //        //Aproximaciones
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='APROXIMACIONES NORTE 04L'";
                //        estadoNorte = Consulta(query2);
                //        IDBOTON = "ID_APROXIMACION";
                //        Cambia_estados_norte(estadoNorte, IDBOTON);
                //        //PAPI
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='PAPI 04L'";
                //        estadoNorte = Consulta(query2);
                //        IDBOTON = "ID_PAPI";
                //        Cambia_estados_norte(estadoNorte, IDBOTON);
                //        //Destello
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='FLASH 04L'";
                //        estadoNorte = Consulta(query2);
                //        IDBOTON = "ID_Destellos";
                //        Cambia_estados_norte(estadoNorte, IDBOTON);
                //    }
                //    else
                //    {
                //        //Aproximaciones
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='APROXIMACIONES NORTE 22R'";
                //        estadoNorte = Consulta(query2);
                //        IDBOTON = "ID_APROXIMACION";
                //        Cambia_estados_norte(estadoNorte, IDBOTON);
                //        //PAPI
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='PAPI 22R'";
                //        estadoNorte = Consulta(query2);
                //        IDBOTON = "ID_PAPI";
                //        Cambia_estados_norte(estadoNorte, IDBOTON);
                //        ////Destello
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='FLASH 22R'";
                //        estadoNorte = Consulta(query2);
                //        IDBOTON = "ID_Destellos";
                //        Cambia_estados_norte(estadoNorte, IDBOTON);
                //    }
                //    //TDZ 04L
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='TDZ 04L'";
                //    estadoNorte = Consulta(query2);
                //    IDBOTON = "ID_TDZ";
                //    Cambia_estados_norte(estadoNorte, IDBOTON);
                //    //BORDES 04L y 22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BORDE DE PISTA NORTE'";
                //    estadoNorte = Consulta(query2);
                //    IDBOTON = "ID_BORDES";
                //    Cambia_estados_norte(estadoNorte, IDBOTON);
                //    //EJES 04L y 22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE DE PISTA NORTE'";
                //    estadoNorte = Consulta(query2);
                //    IDBOTON = "ID_EJE";
                //    Cambia_estados_norte(estadoNorte, IDBOTON);
                //}
                //#endregion

                //#region Central
                ////Pista central
                //if (contador == 170)
                //{
                //    if (Cabecera_activa_centro == "idCabeceraL")
                //    {
                //        //Aproximaciones
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='APROXIMACIONES 04C'";
                //        estadoCentro = Consulta(query2);
                //        IDBOTON = "ID_APROXIMACION";
                //        Cambia_estados_centro(estadoCentro, IDBOTON);
                //        //PAPI
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='PAPI 04C'";
                //        estadoCentro = Consulta(query2);
                //        IDBOTON = "ID_PAPI";
                //        Cambia_estados_centro(estadoCentro, IDBOTON);
                //        //Destello
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='FLASH 04C'";
                //        estadoCentro = Consulta(query2);
                //        IDBOTON = "ID_Destellos";
                //        Cambia_estados_centro(estadoCentro, IDBOTON);
                //    }
                //    else
                //    {
                //        //Aproximaciones
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='APROXIMACIONES 22C'";
                //        estadoCentro = Consulta(query2);
                //        IDBOTON = "ID_APROXIMACION";
                //        Cambia_estados_centro(estadoCentro, IDBOTON);
                //        //PAPI
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='PAPI 22C'";
                //        estadoCentro = Consulta(query2);
                //        IDBOTON = "ID_PAPI";
                //        Cambia_estados_centro(estadoCentro, IDBOTON);
                //        //Destello
                //        query2 = "select max(status) from sw_estados_reguladores where idBoton='FLASH 22C'";
                //        estadoCentro = Consulta(query2);
                //        IDBOTON = "ID_Destellos";
                //        Cambia_estados_centro(estadoCentro, IDBOTON);
                //    }
                //    //TDZ 04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='TDZ 04C'";
                //    estadoCentro = Consulta(query2);
                //    IDBOTON = "ID_TDZ";
                //    Cambia_estados_centro(estadoCentro, IDBOTON);
                //    //BORDES 04C y 22C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BORDE DE PISTA CENTRO'";
                //    estadoCentro = Consulta(query2);
                //    IDBOTON = "ID_BORDES";
                //    Cambia_estados_centro(estadoCentro, IDBOTON);
                //    //EJES 04C y 22C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE DE PISTA CENTRO'";
                //    estadoCentro = Consulta(query2);
                //    IDBOTON = "ID_EJE";
                //    Cambia_estados_centro(estadoCentro, IDBOTON);
                //}
                //#endregion

                //#region Rodajes
                ////Rodajes 1
                //if (contador == 171)
                //{
                //    //Barras de parada K04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BARRAS DE PARADA K04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_BARRAS_K2_K7";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //Barras de parada PQ-04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BARRAS DE PARADA PQ-04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BARRAS_DE_PARADA_PQ04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //Barras de parada K22C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BARRAS DE PARADA K22C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_BARRAS_K11_K17";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //Barras de parada A04L
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BARRAS DE PARADA A04L'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_BARRAS_A1_A6";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //Barras de parada PQ04L
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BARRAS DE PARADA PQ04L'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BARRAS_DE_PARADA_PQ04L";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //Barras de parada A22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BARRAS DE PARADA A22R'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_BARRAS_A8_A15";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE A04L 1
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE A04L'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_A04L";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE A04L 2
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE A04L2'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_A04L2";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE APQ 22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE APQ 22R'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_APQ22R";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE AB 22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE AB 22R'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_AB22R";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE B04L 1
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE B04L'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_B04L";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE B04L 2
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE B04L2'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_B04L2";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE BPQ22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE BPQ22R'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_BPQ22r";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE CC
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE CC'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_CC";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE EF04L
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE EF04L'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EF04L";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE FE04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE FE04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_FE04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE HK04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE HK04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_HK04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE HJ04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE HJ04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_HJ04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE J04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE J04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_J04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE JPQ04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE JPQ04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_JPQ04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE JJ22C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE JJ22C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_JJ22C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE J22C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE J22C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_J22C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE K04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE K04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_K04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //}
                //#endregion

                //#region Rodajes 2
                ////Rodajes 2
                //if (contador == 172)
                //{
                //    //EJE KPQ04C22C 1
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE KPQ04C22C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_KPQ04C22C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE KPQ04C22C 2
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE KPQ04C22C2'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_KPQ04C22C2";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE K22C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE K22C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_K22C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE PQ22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE PQ22R'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_PQ22R";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE PQ04C
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE PQ04C'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_PQ04C";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //BORDES NORTE
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BORDES NORTE'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_BORDES_NORTE";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //BORDES CENTRO
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='BORDES CENTRO'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_BORDES_CENTRAL";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //LETREROS NORTE
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='LETREROS NORTE'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_LETREROS_NORTE";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //LETREROS CENTRO
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='LETREROS CENTRO'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_LERTREROS_CENTRAL";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE Z
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE Z'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_Z";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //TODOS LOS ATRAQUES
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='TODOS LOS ATRAQUES'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_ATRAQUES";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE B22R
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE B22R'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_EJE_B22R";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS NORTE RETA6
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS NORTE RETA6'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_RET_A6";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS NORTE RETA8
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS NORTE RETA8'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_RET_A8";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS NORTE TLO A1-A5
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS NORTE TLO A1-A5'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_TLO_A1_A5";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS NORTE TLO A9-A15
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS NORTE TLO A9-A15'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_TLO_A9_A15";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS CENTRO RET K7
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS CENTRO RET K7'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_RET_K7";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS CENTRO RET K11
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS CENTRO RET K11'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_RET_K11";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS CENTRO TLO K2-K6
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS CENTRO TLO K2-K6'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_TLO_K2_K6";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //    //EJE SALIDA RAPIDAS CENTRO TLO K14-K17
                //    query2 = "select max(status) from sw_estados_reguladores where idBoton='EJE SALIDA RAPIDAS CENTRO TLO K14-K17'";
                //    estadoRodaje = Consulta(query2);
                //    IDBOTON = "ID_BOTON_SALIDAS_TLO_K14_K17";
                //    Cambia_estados_rodajes(estadoRodaje, IDBOTON);
                //}
                //#endregion


                //if (contador == 173)
                //{
                //    contador = 0;
                //    oTimer.Interval = Convert.ToInt32(ConfigurationManager.AppSettings["intervalosEjecucion"]);
                //}
                //else
                //{
                //    //Consulta_Comunicacion(query1);
                //}

                //contador++;                
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
