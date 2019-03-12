﻿using System;
using System.Configuration;
using System.Collections.Generic;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using System.DirectoryServices.AccountManagement;
using CountableSystem.Module.BusinessObjects.Catalog;

namespace CountableSystem.Module.BusinessObjects.Security
{
    public class Autenticacion : AuthenticationBase, IAuthenticationStandard
    {

        private ParametroAcceso customLogonParameters;
        public Autenticacion()
        {
            customLogonParameters = new ParametroAcceso();
        }
        public override void Logoff()
        {
            base.Logoff();
            customLogonParameters = new ParametroAcceso();
        }
        public override void ClearSecuredLogonParameters()
        {
            customLogonParameters.Clave = "";
            base.ClearSecuredLogonParameters();
        }
        public override object Authenticate(IObjectSpace objectSpace)
        {

            ParametroAcceso LosParametrosDeAcceso = (ParametroAcceso)(customLogonParameters);
            if (String.IsNullOrEmpty(LosParametrosDeAcceso.Usuario))
            {
                throw new ArgumentException("Usuario");
            }

            Usuario UsuarioActual;

            CriteriaOperator Criteria = new BinaryOperator("UserName", LosParametrosDeAcceso.Usuario, BinaryOperatorType.Equal);

            var UsuarioObject = objectSpace.FindObject(typeof(Usuario), Criteria, false);
            UsuarioActual = (Usuario)UsuarioObject;

            if (UsuarioActual == null)
            {
                throw new UserFriendlyException("El usuario no existe, comuniquese con el administrador");
            }

            if (String.IsNullOrEmpty(LosParametrosDeAcceso.Clave) || String.IsNullOrWhiteSpace(LosParametrosDeAcceso.Clave))
            {
                throw new UserFriendlyException("La clave esta vacia, favor ingrese clave");
            }

            // Validando el codigo de empresa ingresado

            if (LosParametrosDeAcceso.CodigoEmpresa != null && LosParametrosDeAcceso.CodigoEmpresa != string.Empty)
            {
                Criteria = CriteriaOperator.And(new BinaryOperator("Codigo", LosParametrosDeAcceso.CodigoEmpresa));
                Empresa ObjetoEmpresa = objectSpace.FindObject<Empresa>(Criteria, false);

                if (ObjetoEmpresa == null)
                {
                    throw new UserFriendlyException("Codigo de empresa no registrado, verifique");
                }
            }


            ContainsOperator CriteriaContain = new ContainsOperator("Empresas", new BinaryOperator("Codigo",LosParametrosDeAcceso.CodigoEmpresa));
            UsuarioActual.Empresas.Criteria = CriteriaContain;

            if (UsuarioActual.Empresas.Count == 0)
                throw new UserFriendlyException("El usuario no tiene acceso a esta empresa, verifique");


            var MySettingKey = (string)(ConfigurationManager.AppSettings["ModoAutenticacion"]);
            int ModoAutenticacion = Convert.ToInt32(MySettingKey);

            string DominioAutenticacion = ConfigurationManager.AppSettings["DominioDeAutenticacion"];

            if (UsuarioActual != null)
            {
                RevisarUsuarioActivo(UsuarioActual);

                switch (ModoAutenticacion)
                {

                    case 1: // Autentica con los usuarios y claves locales
                        ComparacionClaveInterna(LosParametrosDeAcceso, UsuarioActual);
                        break;
                    case 2: // Autentica con los usuarios y claves del dominio
                        ComparacionClaveActiveDirectory(LosParametrosDeAcceso, ContextType.Domain, DominioAutenticacion);
                        break;
                    case 3: // Autentica con los usuarios y claves de la pc
                        ComparacionClaveActiveDirectory(LosParametrosDeAcceso, ContextType.Machine, DominioAutenticacion);
                        break;
                    default:
                        throw new UserFriendlyException("Debe definir el metodo de autenticación, consulte al administrador");
                }
            }

            return UsuarioActual;
        }

        public override void SetLogonParameters(object logonParameters)
        {
            this.customLogonParameters = (ParametroAcceso)logonParameters;
        }

        public override IList<Type> GetBusinessClasses()
        {
            return new Type[] { typeof(ParametroAcceso) };
        }
        public override bool AskLogonParametersViaUI
        {
            get { return true; }
        }
        public override object LogonParameters
        {
            get { return customLogonParameters; }
        }
        public override bool IsLogoffEnabled
        {
            get { return true; }
        }

        public static Boolean RevisarUsuarioActivo(Usuario ElUsuario)
        {
            if (ElUsuario.IsActive == false)
            {
                throw new UserFriendlyException(String.Format("El Usuario {0} no esta activo, consulte con el administrador", ElUsuario.UserName));
            }
            return true;
        }

        private void ComparacionClaveInterna(ParametroAcceso losParametrosDeAcceso, Usuario usuarioActual)
        {
            if (usuarioActual.ComparePassword(losParametrosDeAcceso.Clave) == false)
            {
                throw new AuthenticationException(losParametrosDeAcceso.Usuario, "Clave incorrecta, verifique");
            }
        }

        private void ComparacionClaveActiveDirectory(ParametroAcceso losParametrosDeAcceso, ContextType contextType, string dominioAutenticacion)
        {

            PrincipalContext ElEquipo = new PrincipalContext(contextType, dominioAutenticacion);

            if (ElEquipo.ValidateCredentials(losParametrosDeAcceso.Usuario, losParametrosDeAcceso.Clave) == false)
            {
                throw new AuthenticationException(losParametrosDeAcceso.Usuario, "Clave incorrecta, verifique");
            }
        }


    }
}
