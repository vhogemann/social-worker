import React from "react";
import { Button } from "./Button";

export interface PaginationProps {
  currentPage: number;
  totalPages: number;
  totalItems?: number;
  onPageChange: (page: number) => void;
  className?: string;
}

export const Pagination: React.FC<PaginationProps> = ({
  currentPage,
  totalPages,
  totalItems,
  onPageChange,
  className = ""
}) => {
  if (totalPages <= 1) return null;

  return (
    <div
      className={`flex justify-between items-center border-t border-border pt-3 text-xs text-muted ${className}`}
    >
      <span>
        Page {currentPage} of {totalPages}
        {totalItems !== undefined && ` (${totalItems} items)`}
      </span>
      <div className="flex gap-1">
        <Button
          variant="outline"
          size="xs"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
        >
          Prev
        </Button>
        <Button
          variant="outline"
          size="xs"
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  );
};
