interface IconProps {
  name: string;
  className?: string;
}

/** Material Symbols Outlined icon. Decorative by default (aria-hidden). */
export function Icon({ name, className }: IconProps) {
  return (
    <span className={className ? `material-symbols-outlined ${className}` : 'material-symbols-outlined'} aria-hidden="true">
      {name}
    </span>
  );
}
