// Copyright (c) Valdis Iljuconoks. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace DbLocalizationProvider.AspNetCore.DataAnnotations
{
    /// <summary>
    /// This type is acting in view model localization pipeline
    /// </summary>
    public class LocalizedDisplayMetadataProvider : IDisplayMetadataProvider
    {
        private readonly ModelMetadataLocalizationHelper _metadataHelper;

        /// <summary>
        /// Initiates new instance of this helper.
        /// </summary>
        /// <param name="metadataHelper">Metadata helper</param>
        public LocalizedDisplayMetadataProvider(ModelMetadataLocalizationHelper metadataHelper)
        {
            _metadataHelper = metadataHelper;
        }

        /// <summary>
        /// Called by ASP.NET Core when we need to translate view model display labels
        /// </summary>
        /// <param name="context"></param>
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            var theAttributes = context.Attributes;
            var modelMetadata = context.DisplayMetadata;
            var propertyName = context.Key.Name;
            var containerType = context.Key.ContainerType;

            if(containerType == null) return;

            var currentMetaData = modelMetadata.DisplayName?.Invoke();
            if (currentMetaData == null) return;

            modelMetadata.DisplayName = () => !ConfigurationContext.Current.ResourceLookupFilter(currentMetaData)
                ? _metadataHelper.GetTranslation(currentMetaData)
                : _metadataHelper.GetTranslation(containerType, propertyName);

            var displayAttribute = theAttributes.OfType<DisplayAttribute>().FirstOrDefault();
            if(displayAttribute?.Description != null)
            {
                modelMetadata.Description = () =>
                    _metadataHelper.GetTranslation(containerType, $"{propertyName}-Description");
            }
        }
    }
}
