//===============================================================================================================
// System  : Sandcastle Help File Builder
// File    : themeSwitcher.js
// Author  : Eric Woodruff  (Eric@EWoodruff.us)
// Updated : 12/19/2025
// Note    : Copyright 2025, Eric Woodruff, All rights reserved
//
// This file contains the methods necessary to implement theme switching between light and dark modes
//
// This code is published under the Microsoft Public License (Ms-PL).  A copy of the license should be
// distributed with the code and can be found at the project website: https://GitHub.com/EWSoftware/SHFB.  This
// notice, the author's name, and all copyright notices must remain intact in all applications, documentation,
// and source files.
//
//    Date     Who  Comments
// ==============================================================================================================
// 12/19/2025  EFW  Created the code
//===============================================================================================================
//
// MailKit modifications: follow the OS color scheme until the user explicitly toggles the theme.  The stock
// version always fell back to light and persisted the theme on every page load, so the OS preference was never
// honored.

// Get the theme preferred by the operating system
function getSystemTheme() {
    if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches)
        return "dark";

    return "light";
}

// Initialize the theme on page load
function initializeTheme() {
    applyTheme(localStorage.getItem("currentTheme") || getSystemTheme());

    // Set up theme switcher button
    const themeSwitcher = document.getElementById("ThemeSwitcher");

    if (themeSwitcher)
        themeSwitcher.addEventListener("click", toggleTheme);

    // Listen for system theme changes
    if (window.matchMedia) {
        const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
        const onSystemThemeChanged = function (e) {
            // Only auto-switch if user hasn't manually set a preference
            if (!localStorage.getItem("currentTheme"))
                applyTheme(e.matches ? "dark" : "light");
        };

        // Modern browsers
        if (mediaQuery.addEventListener)
            mediaQuery.addEventListener("change", onSystemThemeChanged);
        // Older browsers
        else if (mediaQuery.addListener)
            mediaQuery.addListener(onSystemThemeChanged);
    }
}

// Apply the theme to the document
function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
}

// Toggle between light and dark theme and remember the user's choice
function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute("data-theme");
    const newTheme = currentTheme === "light" ? "dark" : "light";

    // If the user toggles back to the OS theme, go back to following the OS
    if (newTheme === getSystemTheme())
        localStorage.removeItem("currentTheme");
    else
        localStorage.setItem("currentTheme", newTheme);

    applyTheme(newTheme);
}

// Apply the theme immediately to prevent a flash of the wrong theme, then finish initialization once the
// DOM is available
applyTheme(localStorage.getItem("currentTheme") || getSystemTheme());

if (document.readyState === "loading")
    document.addEventListener("DOMContentLoaded", initializeTheme);
else
    initializeTheme();