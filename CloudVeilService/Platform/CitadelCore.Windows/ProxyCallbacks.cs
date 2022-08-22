/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace CloudVeilCore.Net.Proxy
{
    /// <summary>
    /// Callback used to make filtering and internet-access decisions for a new traffic flow based on
    /// the application and the port in use.
    /// </summary>
    /// <param name="request">
    /// Firewall request. This contains information about the flow in question.
    /// </param>
    /// <returns>
    /// The response to the firewall inquiry, including the action to take for the given flow, and
    /// possibly for any such future flows that match the same criteria.
    /// </returns>
    public delegate FirewallResponse FirewallCheckCallback(FirewallRequest request);
}