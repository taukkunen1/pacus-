import 'package:flutter/material.dart';

class PacusTheme {
  static const _lightPrimary = Color(0xFF176B5B);
  static const _darkPrimary = Color(0xFF74D7C4);

  static ThemeData light() => _build(Brightness.light);
  static ThemeData dark() => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final scheme = ColorScheme(
      brightness: brightness,
      primary: dark ? _darkPrimary : _lightPrimary,
      onPrimary: dark ? const Color(0xFF06372F) : Colors.white,
      primaryContainer: dark ? const Color(0xFF144A41) : const Color(0xFFD7F2EA),
      onPrimaryContainer: dark ? const Color(0xFFC9F6EA) : const Color(0xFF103E35),
      secondary: dark ? const Color(0xFFFFC978) : const Color(0xFFA95B00),
      onSecondary: dark ? const Color(0xFF472A00) : Colors.white,
      secondaryContainer: dark ? const Color(0xFF5A3A0B) : const Color(0xFFFFE6BF),
      onSecondaryContainer: dark ? const Color(0xFFFFE2B2) : const Color(0xFF4B2B00),
      tertiary: dark ? const Color(0xFFAEC6FF) : const Color(0xFF3F5F9D),
      onTertiary: dark ? const Color(0xFF18315F) : Colors.white,
      tertiaryContainer: dark ? const Color(0xFF263C67) : const Color(0xFFDDE7FF),
      onTertiaryContainer: dark ? const Color(0xFFDCE6FF) : const Color(0xFF1C335F),
      error: dark ? const Color(0xFFFFB4AB) : const Color(0xFFB3261E),
      onError: dark ? const Color(0xFF690005) : Colors.white,
      errorContainer: dark ? const Color(0xFF93000A) : const Color(0xFFF9DEDC),
      onErrorContainer: dark ? const Color(0xFFFFDAD6) : const Color(0xFF410E0B),
      surface: dark ? const Color(0xFF141A19) : const Color(0xFFFBFCFA),
      onSurface: dark ? const Color(0xFFE6ECE9) : const Color(0xFF1A201E),
      surfaceContainerLowest: dark ? const Color(0xFF0C1110) : Colors.white,
      surfaceContainerLow: dark ? const Color(0xFF18201E) : const Color(0xFFF4F7F5),
      surfaceContainer: dark ? const Color(0xFF1C2522) : const Color(0xFFEDF2EF),
      surfaceContainerHigh: dark ? const Color(0xFF26302D) : const Color(0xFFE6ECE8),
      surfaceContainerHighest: dark ? const Color(0xFF303A37) : const Color(0xFFDDE4E0),
      onSurfaceVariant: dark ? const Color(0xFFBEC9C5) : const Color(0xFF56615D),
      outline: dark ? const Color(0xFF82908B) : const Color(0xFF78847F),
      outlineVariant: dark ? const Color(0xFF3D4945) : const Color(0xFFC5CFCA),
      shadow: Colors.black,
      scrim: Colors.black,
      inverseSurface: dark ? const Color(0xFFE6ECE9) : const Color(0xFF2E3532),
      onInverseSurface: dark ? const Color(0xFF28302D) : const Color(0xFFF0F4F2),
      inversePrimary: dark ? _lightPrimary : _darkPrimary,
    );

    final text = Typography.material2021(platform: TargetPlatform.windows)
        .black
        .apply(
          bodyColor: scheme.onSurface,
          displayColor: scheme.onSurface,
          fontFamily: 'Roboto',
          fontFamilyFallback: const ['Segoe UI', 'Arial', 'sans-serif'],
        )
        .copyWith(
          displayLarge: TextStyle(fontSize: 56, height: 1.02, fontWeight: FontWeight.w800, letterSpacing: -1.6, color: scheme.onSurface),
          displayMedium: TextStyle(fontSize: 44, height: 1.05, fontWeight: FontWeight.w800, letterSpacing: -1.2, color: scheme.onSurface),
          headlineLarge: TextStyle(fontSize: 32, height: 1.12, fontWeight: FontWeight.w800, letterSpacing: -.6, color: scheme.onSurface),
          headlineMedium: TextStyle(fontSize: 26, height: 1.16, fontWeight: FontWeight.w800, letterSpacing: -.35, color: scheme.onSurface),
          titleLarge: TextStyle(fontSize: 21, height: 1.2, fontWeight: FontWeight.w800, letterSpacing: -.15, color: scheme.onSurface),
          titleMedium: TextStyle(fontSize: 16, height: 1.3, fontWeight: FontWeight.w700, color: scheme.onSurface),
          bodyLarge: TextStyle(fontSize: 16, height: 1.45, fontWeight: FontWeight.w400, color: scheme.onSurface),
          bodyMedium: TextStyle(fontSize: 14, height: 1.45, fontWeight: FontWeight.w400, color: scheme.onSurface),
          labelLarge: TextStyle(fontSize: 14, height: 1.2, fontWeight: FontWeight.w700, letterSpacing: .1, color: scheme.onSurface),
        );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: dark ? const Color(0xFF0F1513) : const Color(0xFFF5F8F6),
      textTheme: text,
      appBarTheme: AppBarTheme(
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        backgroundColor: Colors.transparent,
        foregroundColor: scheme.onSurface,
        titleTextStyle: text.titleLarge,
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        color: scheme.surfaceContainerLow,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(22),
          side: BorderSide(color: scheme.outlineVariant.withValues(alpha: .55)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerLowest,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
        labelStyle: TextStyle(color: scheme.onSurfaceVariant, fontWeight: FontWeight.w600),
        hintStyle: TextStyle(color: scheme.onSurfaceVariant.withValues(alpha: .75)),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: BorderSide(color: scheme.primary, width: 2),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 68,
        backgroundColor: scheme.surfaceContainerLow,
        indicatorColor: scheme.primaryContainer,
        labelTextStyle: WidgetStatePropertyAll(text.labelLarge),
        iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
          color: states.contains(WidgetState.selected) ? scheme.primary : scheme.onSurfaceVariant,
        )),
      ),
      navigationRailTheme: NavigationRailThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        indicatorColor: scheme.primaryContainer,
        selectedIconTheme: IconThemeData(color: scheme.primary),
        unselectedIconTheme: IconThemeData(color: scheme.onSurfaceVariant),
        selectedLabelTextStyle: text.labelLarge?.copyWith(color: scheme.primary),
        unselectedLabelTextStyle: text.labelLarge?.copyWith(color: scheme.onSurfaceVariant),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          textStyle: text.labelLarge,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, 46),
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          side: BorderSide(color: scheme.outlineVariant),
          textStyle: text.labelLarge,
        ),
      ),
      chipTheme: ChipThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        side: BorderSide(color: scheme.outlineVariant),
        labelStyle: text.labelLarge,
      ),
      dividerTheme: DividerThemeData(color: scheme.outlineVariant.withValues(alpha: .7)),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: scheme.primary,
        linearTrackColor: scheme.surfaceContainerHighest,
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
        titleTextStyle: text.titleLarge,
      ),
    );
  }
}
