module.exports = {
    "Policies": [
        {
            "Id": "a400a00b-f67c-42b7-ba9a-f73d8c67e433",
            "Role": "admin",
            "DisplayName": "Admin",
            "AllowedActions": [
                "UpdateAlarms",
                "DeleteAlarms",
                "CreateDevices",
                "UpdateDevices",
                "DeleteDevices",
                "CreateDeviceGroups",
                "UpdateDeviceGroups",
                "DeleteDeviceGroups",
                "CreateRules",
                "UpdateRules",
                "DeleteRules",
                "CreateJobs",
                "UpdateSimManagement",
                "AcquireToken",
                "CreateDeployments",
                "DeleteDeployments",
                "CreatePackages",
                "DeletePackages",
                "ReadAll",
                "InviteUsers",
                "DeleteUsers",
                "DeleteTenant",
                "EnableAlerting",
                "DisableAlerting",
                "SendC2DMessages",
                "TagPackages"
            ]
        },
        {
            "Id": "e5bbd0f5-128e-4362-9dd1-8f253c6082d7",
            "Role": "readOnly",
            "DisplayName": "Read Only",
            "AllowedActions": ["ReadAll"]
        }
    ]
}
