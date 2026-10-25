using System;
using System.Collections.Generic;
using System.Text;

namespace Nexabode.Services;

public static class Session
{
    // Set at sign-in; recorded in the audit log.
    public static string Administrator { get; set; } = "admin";
}