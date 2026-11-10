import 'package:flutter/material.dart';

class PacusTheme {
  static const _lightPrimary = Color(0xFF006B62);
  static const _darkPrimary = Color(0xFF75E2D0);

  static ThemeData light() => _build(Brightness.light);
  static ThemeData dark() => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final scheme = ColorScheme(
      brightness: brightness,
      primary: dark ? _darkPrimary : _lightPrimary,
      onPrimary: dark ? const Color(0xFF062F2B) : Colors.white,
      primaryContainer: dark ? const Color(0xFF144A45) : const Color(0xFFC1FAED),
      onPrimaryContainer: dark ? const Color(0xFFB8F3E8) : const Color(0xFF003D35),
      secondary: dark ? const Color(0xFFFFD083) : const Color(0xFF805200),
      onSecondary: dark ? const Color(0xFF482C00) : Colors.white,
      secondaryContainer: dark ? const Color(0xFF533A14) : const Color(0xFFFFE8BD),
      onSecondaryContainer: dark ? const Color(0xFFFFE7BC) : const Color(0xFF533400),
      tertiary: dark ? const Color(0xFFD0C4FF) : const Color(0xFF5045A1),
      onTertiary: dark ? const Color(0xFF32256A) : Colors.white,
      tertiaryContainer: dark ? const Color(0xFF3D3466) : const Color(0xFFEEE8FF),
      onTertiaryContainer: dark ? const Color(0xFFEAE4FF) : const Color(0xFF302365),
      error: dark ? const Color(0xFFFFB4AB) : const Color(0xFFB3261E),
      onError: dark ? const Color(0xFF690005) : Colors.white,
      errorContainer: dark ? const Color(0xFF93000A) : const Color(0xFFF9DEDC),
      onErrorContainer: dark ? const Color(0xFFFFDAD6) : const Color(0xFF410E0B),
      surface: dark ? const Color(0xFF141D2B) : const Color(0xFFFAFCFF),
      onSurface: dark ? const Color(0xFFF1F5FC) : const Color(0xFF19263A),
      surfaceContainerLowest: dark ? const Color(0xFF141D2B) : Colors.white,
      surfaceContainerLow: dark ? const Color(0xFF1B2637) : const Color(0xFFFAFCFF),
      surfaceContainer: dark ? const Color(0xFF223046) : const Color(0xFFE9F0F8),
      surfaceContainerHigh: dark ? const Color(0xFF2A3A50) : const Color(0xFFE1EAF4),
      surfaceContainerHighest: dark ? const Color(0xFF34465F) : const Color(0xFFD5E2EF),
      onSurfaceVariant: dark ? const Color(0xFFBAC9DC) : const Color(0xFF4B5C73),
      outline: dark ? const Color(0xFF91A3BA) : const Color(0xFF63748A),
      outlineVariant: dark ? const Color(0xFF465B75) : const Color(0xFFB6C5D6),
      shadow: Colors.black,
      scrim: Colors.black,
      inverseSurface: dark ? const Color(0xFFE5EDF8) : const Color(0xFF26364A),
      onInverseSurface: dark ? const Color(0xFF243246) : const Color(0xFFF1F5FA),
      inversePrimary: dark ? _lightPrimary : _darkPrimary,
    );

    final text = Typography.material2021()
        .black
        .copyWith(
          displayLarge: const TextStyle(fontSize: 54, height: 1.10, fontWeight: FontWeight.w700, letterSpacing: -1.2),
          displayMedium: const TextStyle(fontSize: 42, height: 1.13, fontWeight: FontWeight.w700, letterSpacing: -.8),
          headlineLarge: const TextStyle(fontSize: 32, height: 1.22, fontWeight: FontWeight.w700, letterSpacing: -.4),
          headlineMedium: const TextStyle(fontSize: 26, height: 1.26, fontWeight: FontWeight.w700, letterSpacing: -.2),
          headlineSmall: const TextStyle(fontSize: 23, height: 1.30, fontWeight: FontWeight.w700),
          titleLarge: const TextStyle(fontSize: 21, height: 1.30, fontWeight: FontWeight.w700),
          titleMedium: const TextStyle(fontSize: 17, height: 1.38, fontWeight: FontWeight.w700),
          titleSmall: const TextStyle(fontSize: 15, height: 1.38, fontWeight: FontWeight.w600),
          bodyLarge: const TextStyle(fontSize: 16, height: 1.50),
          bodyMedium: const TextStyle(fontSize: 15, height: 1.50),
          bodySmall: const TextStyle(fontSize: 13, height: 1.45),
          labelLarge: const TextStyle(fontSize: 14, height: 1.35, fontWeight: FontWeight.w700),
          labelMedium: const TextStyle(fontSize: 13, height: 1.35, fontWeight: FontWeight.w600),
          labelSmall: const TextStyle(fontSize: 12, height: 1.35, fontWeight: FontWeight.w600),
        )
        .apply(
          bodyColor: scheme.onSurface,
          displayColor: scheme.onSurface,
          fontFamily: 'Open Sans',
          fontFamilyFallback: const ['Roboto', 'Segoe UI', 'Arial', 'sans-serif'],
        );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: dark ? const Color(0xFF0F1724) : const Color(0xFFF0F4FA),
      fontFamily: 'Open Sans',
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
          borderRadius: BorderRadius.circular(18),
          side: BorderSide(color: scheme.outlineVariant.withValues(alpha: .55)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerLowest,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
        labelStyle: TextStyle(color: scheme.onSurfaceVariant, fontWeight: FontWeight.w600),
        hintStyle: TextStyle(color: scheme.onSurfaceVariant),
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
        labelTextStyle: WidgetStateProperty.resolveWith((states) => text.labelLarge?.copyWith(
          color: states.contains(WidgetState.selected) ? scheme.primary : scheme.onSurfaceVariant,
        )),
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
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.all(12),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        ),
      ),
      checkboxTheme: CheckboxThemeData(
        visualDensity: VisualDensity.standard,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
        side: BorderSide(color: scheme.outline, width: 1.6),
      ),
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
