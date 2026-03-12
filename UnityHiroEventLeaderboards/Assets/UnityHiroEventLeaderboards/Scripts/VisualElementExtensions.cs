// Copyright 2025 The Nakama Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    /// <summary>
    /// Extension methods for VisualElement to provide fail-fast queries and display utilities.
    /// </summary>
    public static class VisualElementExtensions
    {
        /// <summary>
        /// Queries for a required UI element. Throws if not found.
        /// </summary>
        /// <typeparam name="T">The type of VisualElement to find.</typeparam>
        /// <param name="parent">The parent element to search within.</param>
        /// <param name="name">The name of the element to find.</param>
        /// <returns>The found element.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the element is not found.</exception>
        public static T RequireElement<T>(this VisualElement parent, string name) where T : VisualElement
        {
            var element = parent.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"Required UI element '{name}' of type {typeof(T).Name} not found in {parent.name ?? "root"}");
            }
            return element;
        }

        /// <summary>
        /// Shows the element by setting display to Flex.
        /// </summary>
        public static void Show(this VisualElement element)
        {
            element.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Hides the element by setting display to None.
        /// </summary>
        public static void Hide(this VisualElement element)
        {
            element.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Sets the display style based on a boolean condition.
        /// </summary>
        /// <param name="element">The element to modify.</param>
        /// <param name="visible">If true, shows the element; otherwise hides it.</param>
        public static void SetDisplay(this VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
