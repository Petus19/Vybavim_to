/**
 * Prepínanie vzhľadu (svetlý / tmavý / automaticky podľa systému).
 *
 * Voľba sa ukladá v localStorage pod kľúčom `theme`. Na <html> sa nastaví
 * `data-theme="light" | "dark"` a CSS (globals.css) podľa neho prepne farby.
 * „Automaticky“ = kľúč v localStorage chýba a rozhoduje systém.
 */

export type ThemePreference = "system" | "light" | "dark";

export const THEME_STORAGE_KEY = "theme";

/**
 * Spustí sa v <head> ešte pred vykreslením stránky, aby pri načítaní
 * nezablikal nesprávny režim. Musí byť samostatný a bez importov.
 */
export const themeInitScript = `(function(){try{var p=localStorage.getItem("${THEME_STORAGE_KEY}");var d=p==="dark"||(p!=="light"&&matchMedia("(prefers-color-scheme: dark)").matches);var t=d?"dark":"light";var e=document.documentElement;e.dataset.theme=t;e.style.colorScheme=t}catch(_){}})()`;
