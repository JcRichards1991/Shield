﻿/**
 * @ngdoc resource
 * @name UmbracoAccessResource
 * @function
 *
 * @description
 * Handles the Requests for the Umbraco Access area of the custom section
 */
    var apiRoot = 'backoffice/Shield/UmbracoAccessApi/';

    return {
        PostUmbracoAccess: function (model) {
            return $http.post(apiRoot + 'PostUmbracoAccess', angular.toJson(model));
        },
        GetUmbracoAccess: function () {
            return $http.get(apiRoot + 'GetUmbracoAccess');
        }
    };
});