using System;
using System.Text;
using Crestron.SimplSharp.CrestronIO;
using System.Collections.Generic;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharp.CrestronAuthentication;
using Crestron.SimplSharpPro.UI;

namespace AuthenticationExample
{
    public class ControlSystem : CrestronControlSystem
    {
        string sgdFileName = "AuthSettingsPanel.sgd";
        BasicTriListWithSmartObject panel;
         
        ////////////////////////////////////////////////////////////////////////////////
        // Panel state variables. This example program supports one touchpanel controller

        // These flags force the user to type into the text boxes to authenticate.
        // This is the only way to refresh the Output Text serial join. Without this workaround,
        // the user could login once, then keep those same credentials to click through the 
        // authentication check without typing anything into the boxes! The username/pass strings would be saved from the previous login.
        bool typedInUsernameBox;
        bool typedInPasswordBox;

        CTimer authTimeout;
        Authentication.UserToken adminToken; 
        bool hasToken = false;

        // These state variables are specifically for the AddNewGroup dialogue
        Authentication.UserAuthenticationLevelEnum newGroupAccessLevel; // access level chosen by user
        bool isAccessLevelChosen = false; // Indicates whether user has clicked an access level (cannot create group if false)
        /////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()
            : base()
        {
            try
            {
                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);

                // IPID = 3
                panel = new Tsw752(0x3, this);

                // Subscribe to all signal changes from the touchpanel. This handler will be invoked everytime the 
                // user presses a button or types into a text box
                panel.SigChange += new SigEventHandler(Panel_SigChange);
                // If the smart graphics file exists, load it to the panel
                if (File.Exists(Directory.GetApplicationDirectory() + "\\" + sgdFileName))
                {
                    panel.LoadSmartObjects(Directory.GetApplicationDirectory() + "\\" + sgdFileName);
                    
                    // Register the event handler for each Smart Object found
                    foreach (KeyValuePair<uint, SmartObject> so in panel.SmartObjects)
                        so.Value.SigChange += new SmartObjectSigChangeEventHandler(SmartObject_SigChange);
                }
                else
                    ErrorLog.Error("Failed to load SGD file {0} for panel", sgdFileName);

                if (panel.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("Error Registering panel at IPID {0}: {1}", panel.ID, panel.RegistrationFailureReason);

                // Create the timer now, but don't start it until the user logs in
                authTimeout = new CTimer(authTimeoutCallback, Timeout.Infinite); 
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in the constructor: {0}", e);
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        #region Event Handlers
        // Handler for signals received from the touchpanel to the control (aka OUTPUTS).
        // This handler receives ALL possible outputs from the UI. It progressively narrows down
        // which signal was received, then processes it.
        void Panel_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            try
            {
                switch (args.Sig.Type) // digital, analog, or serial output?
                {
                    case eSigType.Bool:
                        if (args.Sig.BoolValue) // respond to rising edge of signal only
                        {
                            if (args.Sig.Number == DigitalOutputJoins.Logoff)
                            {
                                authTimeout.Reset(Timeout.Infinite);
                                return;
                            }
                            // Click an authentication operation
                            if (args.Sig.Number >= DigitalOutputJoins.ClickAddUserToGroup
                                && args.Sig.Number <= DigitalOutputJoins.ClickSeeAllUsers)
                            {
                                if (args.Sig.Number == DigitalOutputJoins.ClickSeeAllUsers)
                                {
                                    SeeAllUsers(); // side effect of this method writes user list (or an error message) to the screen
                                }
                                if (args.Sig.Number == DigitalOutputJoins.ClickSeeAllGroups)
                                {
                                    SeeAllGroups(); // Same explanation as for SeeAllUsers()
                                }
                                // Open the dialogue corresponding to the clicked button
                                OpenDialogue(args.Sig.Number - Offsets.ClickJoinToVisibilityJoinOffset);
                                authTimeout.Reset(5 * 60 * 1000);
                                return;
                            }
                            switch (args.Sig.Number)
                            {
                                // Navigation to and from the Start, Admin Login, Authentication Settings, and Timeout pages
                                case DigitalOutputJoins.GoToAuthSettings:
                                    // User navigated to Authentication Settings page from the Start Page OR from the Timeout subpage.
                                    // This means it's time to authenticate
                                    ClearAllErrorMessages();
                                    panel.StringInput[SerialInputJoins.ClearAllTextFields].StringValue = "";
                                    panel.BooleanInput[DigitalInputJoins.TimeoutVisible].BoolValue = false; // In the event that we're logging in again after a timeout
                                    panel.BooleanInput[DigitalInputJoins.AdminLoginVisible].BoolValue = true;
                                    break;
                                case DigitalOutputJoins.SubmitAdminCredentials:
                                    string username = panel.StringOutput[SerialOutputJoins.AdminLoginUsername].StringValue;
                                    string pass = panel.StringOutput[SerialOutputJoins.AdminLoginPassword].StringValue;

                                    if (!(typedInPasswordBox && typedInUsernameBox))
                                    {
                                        panel.StringInput[SerialInputJoins.AdminLoginErrMsg].StringValue =
                                            "Authentication Failed";
                                        break;
                                    }

                                    if (CreateToken(username, pass))
                                    {
                                        // authenticated successfully. Remove the Login subpage and begin the 5-minute timer
                                        // The touchpanel transitions directly to the Authentication Settings view, rather than showing
                                        // the "Operation was successful" subpage
                                        panel.BooleanInput[DigitalInputJoins.AdminLoginVisible].BoolValue = false;

                                        // make sure the program clears the username/password boxes immediately after authenticating
                                        panel.StringInput[SerialInputJoins.ClearAllTextFields].StringValue = "";
                                        typedInUsernameBox = false;
                                        typedInPasswordBox = false;
                                    }
                                    break;

                                case DigitalOutputJoins.BackToSettings:
                                    isAccessLevelChosen = false; // In case user clicked back button from AddNewGroup dialogue
                                    CloseAllDialogues();
                                    break;

                                // Dialogues (subpages for specific authentication-related operations)
                                case DigitalOutputJoins.CreateNewUser:
                                    if (AddNewUser(panel.StringOutput[SerialOutputJoins.NewUserName].StringValue,
                                                   panel.StringOutput[SerialOutputJoins.NewUserPass].StringValue,
                                                   panel.StringOutput[SerialOutputJoins.NewUserVerifyPass].StringValue))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.AddNewUserVisible);
                                    }
                                    break;

                                case DigitalOutputJoins.CreateNewGroup:
                                    if (AddNewGroup(panel.StringOutput[SerialOutputJoins.NewGroupName].StringValue,
                                                    newGroupAccessLevel))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.AddNewGroupVisible);
                                    }
                                    isAccessLevelChosen = false; // set back to false for further group creations
                                    break;
                                case DigitalOutputJoins.AddToGroup:
                                    if (MyAddUserToGroup(panel.StringOutput[SerialOutputJoins.AddUserToGroupUsername].StringValue,
                                                         panel.StringOutput[SerialOutputJoins.AddUserToGroupGroupname].StringValue))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.AddUserToGroupVisible);
                                    }
                                    break;
                                case DigitalOutputJoins.ChangePassword:
                                    if (ChangeUserPassword(panel.StringOutput[SerialOutputJoins.ChangePassUsername].StringValue,
                                                           panel.StringOutput[SerialOutputJoins.ChangePassOldPass].StringValue,
                                                           panel.StringOutput[SerialOutputJoins.ChangePassNewPass].StringValue,
                                                           panel.StringOutput[SerialOutputJoins.ChangePassVerifyNew].StringValue))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.ChangeUserPasswordVisible);
                                    }
                                    break;
                                case DigitalOutputJoins.DeleteGroup:
                                    if (MyDeleteGroup(panel.StringOutput[SerialOutputJoins.DeleteGroupName].StringValue))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.DeleteGroupVisible);
                                    }
                                    break;
                                case DigitalOutputJoins.DeleteUser:
                                    if (MyDeleteUser(panel.StringOutput[SerialOutputJoins.DeleteUserName].StringValue))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.DeleteUserVisible);
                                    }
                                    break;
                                case DigitalOutputJoins.RemoveFromGroup:
                                    if (MyRemoveUserFromGroup(panel.StringOutput[SerialOutputJoins.RemoveUserName].StringValue,
                                                              panel.StringOutput[SerialOutputJoins.RemoveUserGroupname].StringValue))
                                    {
                                        ShowSuccessMessage(DigitalInputJoins.RemoveUserFromGroupVisible);
                                    }
                                    break;

                                // Success message after completing an authetnication operation successfully
                                case DigitalOutputJoins.SuccessMsgPressOk:
                                    // user pressed OK. Remove the success message from the screen
                                    panel.BooleanInput[DigitalInputJoins.SuccessMessageVisible].BoolValue = false;
                                    break;
                            }
                        }
                        break;
                    case eSigType.UShort: // no Analog outputs from panel
                        break;
                    case eSigType.String:
                        switch (args.Sig.Number)
                        {
                            case SerialOutputJoins.AdminLoginUsername:
                                typedInUsernameBox = true;
                                break;
                            case SerialOutputJoins.AdminLoginPassword:
                                typedInPasswordBox = true;
                                break;
                        }
                        break;
                }
                // reset the timer when the user provides any of these outputs
                authTimeout.Reset(5 * 60 * 1000);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in Panel_SigChange: {0}", e);
            }
            finally
            {

            }
        }

        void LatchButton(uint id) // select and latch the button pressed and deselect all others. id = 0 to deselect all buttons
        {
            panel.SmartObjects[SmartObjectIDs.AccessLevelList].UShortInput[AccessLevelList.SelectButton].UShortValue = (ushort) id;
            for (ushort i = 1; i < 5; i++)
            {
                if (i == id)
                    continue;
                panel.SmartObjects[SmartObjectIDs.AccessLevelList].UShortInput[AccessLevelList.DeselectButton].UShortValue = i;
            }
        }


        void SmartObject_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            // The control system handles each Smart Object's own signal changes according to its respective Smart Object ID.
            // The SmartObject_SigChange handler should therefore read this ID via args.SmartObjectArgs.ID to determine 
            // which Smart Object has sent a signal change. From there, the program must use the Number property of the sig
            // to determine which component of the Smart Object sent the signal
            try
            {
                switch (args.SmartObjectArgs.ID) // which Smart Object?
                {
                    // The touchpanel uses a Button list Smart Object to display the 5 access levels
                    case SmartObjectIDs.AccessLevelList:
                        if (args.Sig.BoolValue) // only need to process the signal's "rising edge" (click, not the unclick)
                        {
                            LatchButton(args.Sig.Number);
                            switch (args.Sig.Number) // which component of the Smart Object?
                            {
                                case AccessLevelList.Connection:
                                    newGroupAccessLevel = Authentication.UserAuthenticationLevelEnum.Connect;
                                    break;
                                case AccessLevelList.User:
                                    newGroupAccessLevel = Authentication.UserAuthenticationLevelEnum.User;
                                    break;
                                case AccessLevelList.Operator:
                                    newGroupAccessLevel = Authentication.UserAuthenticationLevelEnum.Operator;
                                    break;
                                case AccessLevelList.Programmer:
                                    newGroupAccessLevel = Authentication.UserAuthenticationLevelEnum.Programmer;
                                    break;
                                case AccessLevelList.Administrator:
                                    newGroupAccessLevel = Authentication.UserAuthenticationLevelEnum.Administrator;
                                    break;
                                default:
                                    newGroupAccessLevel = Authentication.UserAuthenticationLevelEnum.NoAccess;
                                    break;
                            }
                        }
                        isAccessLevelChosen = true;
                        break;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in SmartObject_SigChange: {0}", e);
            }
            finally
            {

            }
        }

        void authTimeoutCallback(object userSpecific)
        {
            // 5 minutes of inactivity have passed. Activate timeout page and request re-authentication
            OpenDialogue(DigitalInputJoins.TimeoutVisible);
            Authentication.ReleaseAuthenticationToken(adminToken);
            hasToken = false;
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    authTimeout.Dispose();
                    panel.Dispose();
                    break;
            }
        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    authTimeout.Dispose();
                    panel.Dispose();
                    break;
            }
        }
        #endregion

        #region Helper Functions
        // Clear all text boxes and messages regardless of which dialogue was opened, then make the 
        // requested subpage appear on the screen
        void OpenDialogue(uint visibilityJoin)
        {
            ClearAllErrorMessages(); 
            panel.StringInput[SerialInputJoins.ClearAllTextFields].StringValue = "";
            panel.BooleanInput[visibilityJoin].BoolValue = true;
        }

        // Transition to "Operation Successful" subpage
        void ShowSuccessMessage(uint visibilityJoin)
        {
            panel.BooleanInput[visibilityJoin].BoolValue = false;
            panel.BooleanInput[DigitalInputJoins.SuccessMessageVisible].BoolValue = true;
        }

        // Close all operation subpages when user clicks "Back to Settings"
        void CloseAllDialogues()
        {
            for (uint i = DigitalInputJoins.AddUserToGroupVisible; i <= DigitalInputJoins.SeeAllUsersVisible; i++)
            {
                panel.BooleanInput[i].BoolValue = false;
            }
        }

        void ClearAllErrorMessages()
        {
            for (uint i = SerialInputJoins.AdminLoginErrMsg; i <= SerialInputJoins.AddUserToGroupErrMsg; i++)
            {
                panel.StringInput[i].StringValue = "";
            }
        }
        bool AdminCheck(Authentication.UserToken token)
        {
            return token.Valid &&
                ((token.Access & Authentication.UserAuthenticationLevelEnum.Administrator)
                == Authentication.UserAuthenticationLevelEnum.Administrator);
        }
        // Print an error message to the control system's log AND to the specified Serial input on the touchpanel
        void WriteError(string method, Exception e, uint join)
        {
            try
            {
                panel.StringInput[join].StringValue = String.Format("Error: {0}", e.Message);
            }
            catch
            {
                CrestronConsole.PrintLine("Could not send error to touchpanel");
            }
            finally
            {
                CrestronConsole.PrintLine("Error in {0}: {1}", method, e);
            }
        }
        #endregion

        #region Authentication Settings Operations
        // On Authentication Settings Page user can:
        // 1. Add new user                  
        // 2. Add new group                 
        // 3. See all Users                 
        // 4. See all Groups                
        // 5. Change user passwords         
        // 6. Add user to group             
        // 7. Remove user from group        
        // 8. Delete User (if possible)     
        // 9. Delete Group (if possible)    

        // Creates a token for the admin user, which the other methods will check, or prints error message
        public bool CreateToken(string userName, string password)
        {
            try
            {
                if (hasToken)
                {
                    Authentication.ReleaseAuthenticationToken(adminToken);
                    hasToken = false;
                }
                if (Authentication.ValidateUserInformation(userName, password).Authenticated)
                {
                    adminToken = Authentication.GetAuthenticationToken(userName, password);
                    if (adminToken.Valid)
                    {
                        if ((adminToken.Access & Authentication.UserAuthenticationLevelEnum.Administrator)
                                == Authentication.UserAuthenticationLevelEnum.Administrator)
                        {
                            CrestronConsole.PrintLine("Admin Token created\r\n");
                            hasToken = true;
                            return true;
                        }
                        else
                        {
                            panel.StringInput[SerialInputJoins.AdminLoginErrMsg].StringValue = 
                                "Inputted credentials are not for an admin account";
                            Authentication.ReleaseAuthenticationToken(adminToken);
                            return false;
                        }
                    }
                    else
                    {
                        panel.StringInput[SerialInputJoins.AdminLoginErrMsg].StringValue = 
                                "The specified username/password pair does not exist on the system";
                        return false;
                    }
                }
                else
                {
                    panel.StringInput[SerialInputJoins.AdminLoginErrMsg].StringValue = 
                                "Unable to validate user";
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("CreateToken", e, SerialInputJoins.AdminLoginErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public bool AddNewUser(string username, string password, string verify)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.AddNewUserErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                if (password != verify) // string comparison
                {
                    panel.StringInput[SerialInputJoins.AddNewUserErrMsg].StringValue =
                        "Error: Password and Verify Password do not match";
                    return false;
                }
                if (Authentication.AddUserToSystem(ref adminToken, username, password))
                {
                    return true;                    
                }
                else
                {
                    panel.StringInput[SerialInputJoins.AddNewUserErrMsg].StringValue = 
                        String.Format("{0} could not be added to the system", username);
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("AddNewUser", e, SerialInputJoins.AddNewUserErrMsg);
                return false;
            }
            finally
            {

            }
        }

        // Add a new group to the control system, or print an error message to the touchpanel
        public bool AddNewGroup(string groupName, Authentication.UserAuthenticationLevelEnum accessLevel)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.AddNewGroupErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                if (!isAccessLevelChosen)
                {
                    panel.StringInput[SerialInputJoins.AddNewGroupErrMsg].StringValue =
                        "You must choose an access level for this new group";
                    return false;
                }
                CrestronConsole.PrintLine("Creating group {0} with access level {1} ({2})...\r\n", groupName, accessLevel, (Int32)accessLevel);
                if (Authentication.AddGroupToSystem(ref adminToken, groupName, accessLevel))
                {
                    CrestronConsole.PrintLine("Group \"{0}\" added", groupName);
                    return true;
                }
                panel.StringInput[SerialInputJoins.AddNewGroupErrMsg].StringValue = 
                        String.Format("Group \"{0}\" could not be added", groupName);
                CrestronConsole.PrintLine("Group \"{0}\" could not be added", groupName);
                return false;
            }
            catch (Exception e)
            {
                WriteError("AddNewGroup", e, SerialInputJoins.AddNewGroupErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public bool SeeAllUsers()
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.UsersList].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                // Quickly append each user to the list with a StringBuilder, 
                // then convert the finished list to a string and send it to the panel
                StringBuilder sb = new StringBuilder(); 
                List<string> users = Authentication.GetUsers(adminToken);
                if (users == null)
                {
                    sb.Append("An error has occurred when trying to list the users");
                }
                else if (users.Count == 0) 
                {
                    sb.Append("No Users\r");
                }
                else
                {
                    sb.Append(String.Format("{0,-10}{1,-15}\r\r", "User", "Access Level"));
                    foreach (var user in users)
                    {
                        sb.Append(String.Format("{0,-10}", user));
                        sb.Append(String.Format("{0,-15}", Authentication.GetAccessLevelForSpecifiedUser(adminToken, user)));
                        // Implement a "whoami" capability. Indicate the currently authenticated user in the list
                        if (user == adminToken.UserName)
                        {
                            sb.Append("     *Current User");
                        }
                        sb.Append("\r");
                    }
                }
                panel.StringInput[SerialInputJoins.UsersList].StringValue = sb.ToString();
                return true;
            }
            catch (Exception e)
            {
                // The user list text box shows the error message when necessary
                WriteError("SeeAllUsers", e, SerialInputJoins.UsersList); 
                return false;
            }
            finally
            {

            }
        }
        // No Authentication method lists the groups
        public bool SeeAllGroups()
        {
            if (!AdminCheck(adminToken))
            {
                panel.StringInput[SerialInputJoins.GroupsList].StringValue =
                    "You do not have sufficient access to perform this operation";
                return false;
            }
            panel.StringInput[SerialInputJoins.GroupsList].StringValue = 
                "Coming Soon... There is currently no \"GetGroups\" method in the Authentication class";
            return true;
        }

        public bool ChangeUserPassword(string username, string oldPass, string newPass, string verifyNewPass)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.ChangePassErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                if (newPass != verifyNewPass)
                {
                    panel.StringInput[SerialInputJoins.ChangePassErrMsg].StringValue =
                        "New Password and Verify New Password do not match";
                    return false;
                }
                if (!Authentication.ValidateUserInformation(username, oldPass).Authenticated)
                {
                    panel.StringInput[SerialInputJoins.ChangePassErrMsg].StringValue =
                        String.Format("Failed to validate {0}", username);
                    return false;
                }
                if (Authentication.UpdatePassword(ref adminToken, username, newPass))
                {
                    CrestronConsole.PrintLine("Password updated for user {0}", username);
                    return true;
                }
                else
                {
                    CrestronConsole.PrintLine("Failed to update password for user {0}", username);
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("ChangeUserPassword", e, SerialInputJoins.ChangePassErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public bool MyAddUserToGroup(string username, string groupname)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.AddUserToGroupErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                if (Authentication.AddUserToGroup(ref adminToken, username, groupname))
                {
                    CrestronConsole.PrintLine("{0} has been added to {1}", username, groupname);
                    return true;
                }
                else
                {
                    panel.StringInput[SerialInputJoins.AddUserToGroupErrMsg].StringValue = 
                        String.Format("{0} could not be added to {1}", username, groupname);
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("MyAddUserToGroup", e, SerialInputJoins.AddUserToGroupErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public bool MyRemoveUserFromGroup(string username, string groupname)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.RemoveUserFromGroupErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                if (Authentication.RemoveUserFromGroup(ref adminToken, username, groupname))
                {
                    CrestronConsole.PrintLine("{0} has been removed from {1}", username, groupname);
                    return true;
                }
                else
                {
                    panel.StringInput[SerialInputJoins.RemoveUserFromGroupErrMsg].StringValue = 
                        String.Format("{0} could not be removed from {1}", username, groupname);
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("MyRemoveUserFromGroup", e, SerialInputJoins.RemoveUserFromGroupErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public bool MyDeleteUser(string username)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.DeleteUserErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                // Do not allow self-deletion
                if (username == adminToken.UserName)
                {
                    panel.StringInput[SerialInputJoins.DeleteUserErrMsg].StringValue =
                        String.Format("Unable to delete {0} because you are " + 
                                      "currently authenticated as this user", username);
                    return false;
                }
                if (Authentication.RemoveUserFromSystem(ref adminToken, username))
                {
                    CrestronConsole.PrintLine("{0} has been deleted", username);
                    return true;
                }
                else
                {
                    panel.StringInput[SerialInputJoins.DeleteUserErrMsg].StringValue = 
                        String.Format("{0} could not be deleted", username);
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("MyDeleteUser", e, SerialInputJoins.DeleteUserErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public bool MyDeleteGroup(string groupname)
        {
            try
            {
                if (!AdminCheck(adminToken))
                {
                    panel.StringInput[SerialInputJoins.DeleteGroupErrMsg].StringValue =
                        "You do not have sufficient access to perform this operation";
                    return false;
                }
                if (Authentication.RemoveGroupFromSystem(ref adminToken, groupname))
                {
                    CrestronConsole.PrintLine("{0} has been deleted", groupname);
                    return true;
                }
                else
                {
                    panel.StringInput[SerialInputJoins.DeleteGroupErrMsg].StringValue = 
                        String.Format("{0} could not be deleted", groupname);
                    return false;
                }
            }
            catch (Exception e)
            {
                WriteError("MyDeleteGroup", e, SerialInputJoins.DeleteGroupErrMsg);
                return false;
            }
            finally
            {

            }
        }

        public void PrintToken(string args)
        {
            if (hasToken)
                CrestronConsole.ConsoleCommandResponse(PrintToken(adminToken));
            else
                CrestronConsole.ConsoleCommandResponse("No Administrator token is registered with the system");
        }

        static string PrintToken(Authentication.UserToken token)
        {
            return String.Format("PrintToken: UN: {0}, Access: {1}, Validity: {2}\r\n",
                token.UserName, token.Access, (token.Valid) ? "Valid" : "Invalid");
        }
        #endregion
    }
}