window.callCadenceAuth = {
    tokenKey: "callcadence.auth.token",
    emailKey: "callcadence.auth.email",
    isAdminKey: "callcadence.auth.isAdmin",
    expiresKey: "callcadence.auth.expiresAtUtc",

    setToken: function (token, email, isAdmin, expiresAtUtc) {
        try {
            localStorage.setItem(this.tokenKey, token ?? "");
            localStorage.setItem(this.emailKey, email ?? "");
            localStorage.setItem(this.isAdminKey, isAdmin ? "true" : "false");
            localStorage.setItem(this.expiresKey, expiresAtUtc ?? "");
        } catch {
            // Storage may be unavailable (private mode); ignore.
        }
    },

    getSession: function () {
        try {
            const token = localStorage.getItem(this.tokenKey);
            if (!token) {
                return null;
            }

            const expiresAtUtc = localStorage.getItem(this.expiresKey);
            if (expiresAtUtc) {
                const expiry = Date.parse(expiresAtUtc);
                if (!isNaN(expiry) && expiry <= Date.now()) {
                    this.clearToken();
                    return null;
                }
            }

            return {
                token: token,
                email: localStorage.getItem(this.emailKey) ?? "",
                isAdmin: localStorage.getItem(this.isAdminKey) === "true",
                expiresAtUtc: expiresAtUtc ?? null
            };
        } catch {
            return null;
        }
    },

    clearToken: function () {
        try {
            localStorage.removeItem(this.tokenKey);
            localStorage.removeItem(this.emailKey);
            localStorage.removeItem(this.isAdminKey);
            localStorage.removeItem(this.expiresKey);
        } catch {
            // ignore
        }
    }
};
