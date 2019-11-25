namespace AuthenticationExample
{
    // This file contains symbolic constants representing descriptive names for all the join numbers
    // Digital = bool, Serial = string
    // Input means (control system ---> touchpanel), 
    // Output means (touchpanel ---> control system)
    public static class DigitalOutputJoins
    {
        public const uint
            Logoff = 1,
            BackToSettings = 2,
            SubmitAdminCredentials = 3,
            CreateNewGroup = 5,
            ChangePassword = 6,
            AddToGroup = 7,
            CreateNewUser = 8,
            RemoveFromGroup = 9,
            DeleteGroup = 10,
            DeleteUser = 11,
            SuccessMsgPressOk = 12,
            GoToAuthSettings = 14,

            // Must be numerically consecutive
            ClickAddUserToGroup = 26,
            ClickAddNewUser = 27,
            ClickAddNewGroup = 28,
            ClickChangeUserPassword = 29,
            ClickDeleteGroup = 30,
            ClickDeleteUser = 31,
            ClickRemoveUserFromGroup = 32,
            ClickSeeAllGroups = 33,
            ClickSeeAllUsers = 34;
    }

    public static class Offsets
    {
        public const uint ClickJoinToVisibilityJoinOffset = 9;
    }

    public static class DigitalInputJoins
    {
        public const uint
            AdminLoginVisible = 4,
            SuccessMessageVisible = 13,
            TimeoutVisible = 16,

            // CloseAllDialogues needs these joins to be consecutive in number. Also, there is
            // a constant offset between the "click" joins and the "dialogue visibility" joins
            AddUserToGroupVisible = 17,
            AddNewUserVisible = 18,
            AddNewGroupVisible = 19,
            ChangeUserPasswordVisible = 20,
            DeleteGroupVisible = 21,
            DeleteUserVisible = 22,
            RemoveUserFromGroupVisible = 23,
            SeeAllGroupsVisible = 24,
            SeeAllUsersVisible = 25;
            // -------------------------
        
    }
    public static class SerialOutputJoins
    {
        public const uint
            AdminLoginUsername = 1,
            AdminLoginPassword = 2,
            NewUserName = 3,
            NewUserPass = 4,
            NewUserVerifyPass = 5,
            NewGroupName = 6,
            ChangePassUsername = 10,
            ChangePassOldPass = 11,
            ChangePassNewPass = 12,
            ChangePassVerifyNew = 13,
            AddUserToGroupUsername = 14,
            AddUserToGroupGroupname = 15,
            RemoveUserName = 16,
            RemoveUserGroupname = 17,
            DeleteGroupName = 18,
            DeleteUserName = 19;
    }   
    public static class SerialInputJoins
    {
        public const uint
            GroupsList = 8,
            UsersList = 9,

            // Must be numerically consecutive
            AdminLoginErrMsg = 20,
            AddNewGroupErrMsg = 21,
            AddNewUserErrMsg = 22,
            ChangePassErrMsg = 23,
            DeleteGroupErrMsg = 24,
            DeleteUserErrMsg = 25,
            RemoveUserFromGroupErrMsg = 26,
            AddUserToGroupErrMsg = 27,
            /////////////////////////

            ClearAllTextFields = 29;
    }
    public static class SmartObjectIDs
    {
        public const uint
            AccessLevelList = 1;
    }

    // Enumerated components of this SmartObject (A Button List with 5 buttons)
    public static class AccessLevelList
    {
        // Boolean outputs (names for each of the 5 buttons)
        public const uint
            Connection = 1,
            User = 2,
            Operator = 3,
            Programmer = 4,
            Administrator = 5;
        // UShort inputs (used for latching the button pressed)
        public const uint
            SelectButton = 1,
            DeselectButton = 2;
    }
}
