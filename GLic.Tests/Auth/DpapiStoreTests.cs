// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Text;
using GLic.Auth;

namespace GLic.Tests.Auth;

public class DpapiStoreTests
{
    [Fact]
    public void RoundTrip_ProtectThenUnprotect_ReturnsSameBytes()
    {
        var original = Encoding.UTF8.GetBytes("{ \"type\": \"service_account\", \"project_id\": \"test\" }");

        var encrypted = DpapiStore.Protect(original);
        var decrypted = DpapiStore.Unprotect(encrypted);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Protect_ProducesOutput_DifferentFromInput()
    {
        var original = Encoding.UTF8.GetBytes("test plaintext");
        var encrypted = DpapiStore.Protect(original);
        Assert.NotEqual(original, encrypted);
    }
}
