import 'package:flutter/material.dart';

class PacusTheme {
  static const _lightPrimary = Color(0xFF007C83);
  static const _darkPrimary = Color(0xFF62E4E8);

  static ThemeData light() => _build(Brightness.light);
  static ThemeData dark() => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final scheme = ColorScheme(
      brightness: brightness,
      primary: dark ? _darkPrimary : _lightPrimary,
      onPrimary: dark ? const Color(0xFF06372F) : Colors.white,
      primaryContainer: dark ? const Color(0xFF003F43) : const Color(0xFFC8F4F1),
      onPrimaryContainer: dark ? const Color(0xFFB9F7F4) : const Color(0xFF003C40),
      secondary: dark ? const Color(0xFFFFA38B) : const Color(0xFFD94F35),
      onSecondary: dark ? const Color(0xFF552014) : Colors.white,
      secondaryContainer: dark ? const Color(0xFF5B2B20) : const Color(0xFFFFDDD3),
      onSecondaryContainer: dark ? const Color(0xFFFFD8CC) : const Color(0xFF5C1D10),
      tertiary: dark ? const Color(0xFFC7B8FF) : const Color(0xFF6554C0),
      onTertiary: dark ? const Color(0xFF31256E) : Colors.white,
      tertiaryContainer: dark ? const Color(0xFF3A316E) : const Color(0xFFE9E2FF),
      onTertiaryContainer: dark ? const Color(0xFFE8E1FF) : const Color(0xFF30236F),
      error: dark ? const Color(0xFFFFB4AB) : const Color(0xFFB3261E),
      onError: dark ? const Color(0xFF690005) : Colors.white,
      errorContainer: dark ? const Color(0xFF93000A) : const Color(0xFFF9DEDC),
      onErrorContainer: dark ? const Color(0xFFFFDAD6) : const Color(0xFF410E0B),
      surface: dark ? const Color(0xFF121719) : const Color(0xFFFFFBF7),
      onSurface: dark ? const Color(0xFFF1F4F4) : const Color(0xFF182022),
      surfaceContainerLowest: dark ? const Color(0xFF090D0F) : Colors.white,
      surfaceContainerLow: dark ? const Color(0xFF171E20) : const Color(0xFFF7F2EC),
      surfaceContainer: dark ? const Color(0xFF1E272A) : const Color(0xFFF0EAE3),
      surfaceContainerHigh: dark ? const Color(0xFF283235) : const Color(0xFFEAE3DB),
      surfaceContainerHighest: dark ? const Color(0xFF333E41) : const Color(0xFFE2D9D0),
      onSurfaceVariant: dark ? const Color(0xFFC5CFD0) : const Color(0xFF596366),
      outline: dark ? const Color(0xFF8B9698) : const Color(0xFF717C7F),
      outlineVariant: dark ? const Color(0xFF414C4F) : const Color(0xFFC5CDCF),
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
          headlineLarge: TextStyle(fontSize: 32, height: 1.15, fontWeight: FontWeight.w800, letterSpacing: -.45, color: scheme.onSurface),
          headlineMedium: TextStyle(fontSize: 26, height: 1.2, fontWeight: FontWeight.w700, letterSpacing: -.25, color: scheme.onSurface),
          titleLarge: TextStyle(fontSize: 21, height: 1.25, fontWeight: FontWeight.w700, letterSpacing: -.10, color: scheme.onSurface),
          titleMedium: TextStyle(fontSize: 16.5, height: 1.35, fontWeight: FontWeight.w700, color: scheme.onSurface),
          bodyLarge: TextStyle(fontSize: 16, height: 1.5, fontWeight: FontWeight.w400, color: scheme.onSurface),
          bodyMedium: TextStyle(fontSize: 14.5, height: 1.5, fontWeight: FontWeight.w400, color: scheme.onSurface),
          labelLarge: TextStyle(fontSize: 14, height: 1.3, fontWeight: FontWeight.w700, letterSpacing: .05, color: scheme.onSurface),
        );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: dark ? const Color(0xFF0B1012) : const Color(0xFFFFF8F1),
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
      listTileTheme: ListTileThemeData(
        minVerticalPadding: 10,
        horizontalTitleGap: 12,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        titleTextStyle: text.titleMedium,
        subtitleTextStyle: text.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
      ),
      checkboxTheme: CheckboxThemeData(
        visualDensity: const VisualDensity(horizontal: 1, vertical: 1),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
        side: BorderSide(color: scheme.outline, width: 1.6),
      ),
      tooltipTheme: TooltipThemeData(
        textStyle: text.bodyMedium?.copyWith(color: scheme.onInverseSurface),
        decoration: BoxDecoration(
          color: scheme.inverseSurface,
          borderRadius: BorderRadius.circular(10),
        ),
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
