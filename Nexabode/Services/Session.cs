using System;
using System.Collections.Generic;
using System.Text;

namespace Nexabode.Services;

public static class Session
{
    // Set at sign-in; recorded in the audit log.
    public static string Administrator { get; set; } = "admin";

    // Set at sign-in: which account is signed in (used to check whether it has a recovery code yet).
    public static int UserId { get; set; }

    // Set at sign-in. Only an Owner can manage accounts and restore a backup.
    public static bool IsOwner { get; set; }
}